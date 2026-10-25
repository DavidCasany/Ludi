using UnityEngine;

// Rigidbody2D en modo Kinematic + un BoxCollider2D (no trigger).
// La posición se ajusta SOLA a la portería real (Goal): línea de gol, centro y anchura.
// Así funciona aunque RinkResizer cambie el tamaño de la pista o de las porterías.
//
//  1) COLOCACIÓN: bisectriz del ángulo bola-palos, saliendo un poco hacia la bola. Recolocarse es más lento.
//  2) TIRO: tarda 'reactionTime' en reaccionar (más si le tapan) y se estira a 'speed'.
//     Si tiene GoalkeeperQTE (portero del humano), manda el QTE.
//  3) REBOTE: la bola sale con parte de su velocidad: hay segundas jugadas.
//
// 'speed' y 'reach' los modifican las mejoras del torneo (TeamStatApplier).
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Goalkeeper : MonoBehaviour
{
    public Team team;

    [Header("Movimiento (lo modifican las mejoras)")]
    [Tooltip("Velocidad de estirada lateral al reaccionar a un tiro (m/s)")]
    public float speed = 7f;
    [Tooltip("Alcance extra más allá del palo (m). 0 = estirado llega justo al palo con el cuerpo")]
    public float reach = 0.2f;

    [Header("Colocación (la portería se mide sola a partir del Goal)")]
    [SerializeField, Range(0.2f, 1f)] float positioningSpeedFactor = 0.6f; // recolocarse = speed x esto
    [SerializeField] float lineOffset = 0.05f;          // separación entre el portero y la línea de gol (m)
    [SerializeField] float maxDepth = 0.6f;             // cuánto sale de la línea hacia la bola (m)
    [SerializeField] float fallbackGoalHalfWidth = 0.85f; // solo si no encuentra ningún Goal

    [Header("Reacción al tiro")]
    [SerializeField] float reactionTime = 0.17f;
    [SerializeField] float screenPenalty = 0.1f;          // reacción extra si un rival se cruza en la línea de tiro
    [SerializeField] float screenRadius = 0.7f;
    [SerializeField] float shotMinSpeed = 9f;             // por debajo no se considera tiro
    [SerializeField, Range(0f, 0.5f)] float readError = 0.12f;   // error al leer por dónde cruzará (m)

    [Header("Parada y rebote")]
    [SerializeField, Range(0f, 1f)] float reboundFactor = 0.45f;
    [SerializeField] float minRebound = 4f;
    [SerializeField] float maxDeflectAngle = 60f;
    [SerializeField] float saveFlashTime = 0.25f;
    [SerializeField] float minReboundAngle = 25f;   // ningún rebote sale recto hacia la zona de remate
    [SerializeField] float qteClearAngle = 65f;     // parada del QTE: despeje hacia la banda (entre esto y 80°)
    [SerializeField] float qteFailExtraDelay = 0.08f;     // QTE fallado: reacciona algo más tarde que la IA
    [SerializeField] float qteFailReadMultiplier = 2.5f;  // ... y lee peor por dónde va la bola
    [SerializeField] float qteMaxDistance = 14f;         // más lejos de esto no hay QTE (es un pase o un tiro lejano)

    public float ReactionTime => reactionTime;
    public float HalfWidth => col != null ? col.bounds.extents.y : 0.4f;   // lo usa TeamBrain para valorar tiros

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    Color baseColor;
    float flashUntil;
    GoalkeeperQTE qte;   // opcional: solo en el portero del humano
    TeamManager owner;
    bool HumanKeeper => owner != null && owner.humanControlled;

    // Geometría real de la portería
    bool fitted;
    float lineX;         // x de la línea de gol (boca)
    float goalCenterY;
    float goalHalf;      // semiancho real de la boca
    Vector2 startPos;    // posición base: centrado y pegado a la línea
    float outwardX;      // +1 o -1: hacia el centro de la pista

    bool tracking;
    float shotTime;
    float shotDelay;
    float readOffset;

    // Desplazamiento lateral máximo del centro: cuerpo tocando el palo + reach
    float LateralLimit => Mathf.Max(0f, goalHalf - HalfWidth) + reach;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        qte = GetComponent<GoalkeeperQTE>();

        startPos = rb.position;
        outwardX = Mathf.Sign(-startPos.x);

        owner = GetComponentInParent<TeamManager>();
    }

    // Se mide con la portería real la primera vez que hace falta (no depende del orden de Awake/OnEnable)
    void EnsureFit()
    {
        if (fitted) return;
        fitted = true;

        Goal g = Goal.Nearest(startPos);
        if (g == null)
        {
            lineX = startPos.x;
            goalCenterY = startPos.y;
            goalHalf = fallbackGoalHalfWidth;
            return;
        }

        Vector2 mouth = g.Mouth;
        outwardX = mouth.x > 0f ? -1f : 1f;
        lineX = mouth.x;
        goalCenterY = mouth.y;
        goalHalf = g.HalfWidth;

        float halfDepth = col.bounds.extents.x;
        startPos = new Vector2(lineX + outwardX * (halfDepth + lineOffset), goalCenterY);
        rb.position = startPos;
        transform.position = startPos;
    }

    // Color del equipo (lo usa el torneo)
    public void SetColor(Color c)
    {
        baseColor = c;
        if (sr != null) sr.color = c;
    }

    void FixedUpdate()
    {
        EnsureFit();

        Ball ball = Ball.Instance;
        Vector2 target = startPos;
        float moveSpeed = speed * positioningSpeedFactor;

        if (ball != null)
        {
            float crossY, timeToLine;
            if (IsShotIncoming(ball, out crossY, out timeToLine))
            {
                if (!tracking)
                {
                    tracking = true;
                    shotTime = Time.time;
                    shotDelay = reactionTime + (IsScreened(ball, crossY) ? screenPenalty : 0f);
                    readOffset = Random.Range(-readError, readError);

                    if (UseQTE(ball, timeToLine)) qte.Begin(Vector2.Distance(ball.rb.position, startPos), timeToLine);
                }

                QTEState q = qte != null ? qte.State : QTEState.Idle;

                if (q == QTEState.Running)
                {
                    target = rb.position;                                   // decidiendo (el tiempo va casi parado)
                }
                else if (q == QTEState.Success)
                {
                    // Parada garantizada: se coloca al instante y, cuando la bola llega a su altura, la despeja él
                    float saveLim = LateralLimit;
                    Vector2 savePos = new Vector2(rb.position.x, Mathf.Clamp(crossY, goalCenterY - saveLim, goalCenterY + saveLim));
                    rb.position = savePos;
                    target = savePos;

                    if ((ball.rb.position.x - rb.position.x) * outwardX < col.bounds.extents.x + 0.6f)
                    {
                        Deflect(ball, ball.Speed);
                        return;
                    }
                }
                else
                {
                    // Portero de la IA, o QTE fallado: reacción normal (si ha fallado, más tarde y leyendo peor)
                    bool failed = q == QTEState.Fail;
                    float delay = shotDelay + (failed ? qteFailExtraDelay : 0f);
                    float error = readOffset * (failed ? qteFailReadMultiplier : 1f);

                    if (Time.time - shotTime < delay)
                    {
                        target = rb.position;
                    }
                    else
                    {
                        target = new Vector2(rb.position.x, crossY + error);
                        moveSpeed = speed;
                    }
                }
            }
            else
            {
                tracking = false;
                if (qte != null && qte.State != QTEState.Idle) qte.Cancel();
                target = PositionTarget(ball.rb.position);
            }
        }

        // Límites: lateral según la portería real, profundidad entre la línea y maxDepth hacia fuera
        float lim = LateralLimit;
        target.y = Mathf.Clamp(target.y, goalCenterY - lim, goalCenterY + lim);
        float minX = Mathf.Min(startPos.x, startPos.x + outwardX * maxDepth);
        float maxX = Mathf.Max(startPos.x, startPos.x + outwardX * maxDepth);
        target.x = Mathf.Clamp(target.x, minX, maxX);

        rb.MovePosition(Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime));

        if (sr != null && Time.time > flashUntil) sr.color = baseColor;
    }

    // Bisectriz del ángulo bola-palos: corta la línea de gol en Y = (d2·P1 + d1·P2) / (d1 + d2)
    Vector2 PositionTarget(Vector2 bp)
    {
        float along = (bp.x - lineX) * outwardX;   // distancia de la bola por delante de la línea

        // Bola casi en la línea de fondo o detrás: pegado al primer palo
        if (along < 0.5f)
        {
            float side = Mathf.Sign(bp.y - goalCenterY);
            return new Vector2(startPos.x, goalCenterY + side * (goalHalf - HalfWidth * 0.9f));
        }

        Vector2 p1 = new Vector2(lineX, goalCenterY - goalHalf);
        Vector2 p2 = new Vector2(lineX, goalCenterY + goalHalf);
        float d1 = Vector2.Distance(bp, p1);
        float d2 = Vector2.Distance(bp, p2);
        Vector2 y = (d2 * p1 + d1 * p2) / (d1 + d2);

        // Sale hacia la bola (reduce el ángulo), menos cuanto más cerrado es el ángulo
        Vector2 dir = (bp - y).normalized;
        float depth = maxDepth * Mathf.Clamp01(Mathf.Abs(dir.x));
        return y + dir * depth;
    }

    // ¿Viene un tiro a PUERTA? Se comprueba en la línea de gol real; devuelve la Y por la que
    // cruzará la línea del portero (que está un poco por delante).
    // ¿Viene un tiro a PUERTA? Devuelve por dónde cruzará la línea del portero y cuánto le falta a la bola.
    bool IsShotIncoming(Ball ball, out float crossY, out float timeToLine)
    {
        crossY = goalCenterY;
        timeToLine = 0f;
        if (ball.IsHeld) return false;

        Vector2 bp = ball.rb.position;
        Vector2 bv = ball.rb.linearVelocity;

        if (bv.sqrMagnitude < shotMinSpeed * shotMinSpeed) return false;
        if (bv.x * outwardX > -0.5f) return false;              // no se acerca a la portería
        if ((bp.x - lineX) * outwardX < 0f) return false;       // ya ha cruzado la línea

        float tLine = (lineX - bp.x) / bv.x;
        float yLine = bp.y + bv.y * tLine;
        if (Mathf.Abs(yLine - goalCenterY) > goalHalf + 0.3f) return false;   // va fuera

        float tKeeper = (rb.position.x - bp.x) / bv.x;
        crossY = tKeeper > 0f ? bp.y + bv.y * tKeeper : yLine;
        timeToLine = tLine;
        return true;
    }

    // QTE para cualquier bola que va a puerta, salvo un pase de nuestro propio equipo
    // QTE solo en el portero del HUMANO, solo con tiros de verdad (no pases) y desde una distancia razonable
    bool UseQTE(Ball ball, float timeToLine)
    {
        if (qte == null || !qte.enabled || !HumanKeeper) return false;
        if (ball.IsPassFor(Team.Home) || ball.IsPassFor(Team.Away)) return false;   // un pase no es un tiro
        if (Vector2.Distance(ball.rb.position, startPos) > qteMaxDistance) return false;
        return timeToLine > 0.02f;
    }

    // Un rival cruzado en la línea de tiro, cerca del portero, le tapa la visión
    bool IsScreened(Ball ball, float crossY)
    {
        Vector2 a = ball.rb.position;
        Vector2 b = new Vector2(lineX, crossY);

        for (int i = 0; i < Skater.All.Count; i++)
        {
            Skater s = Skater.All[i];
            if (s.team == team) continue;

            Vector2 p = s.transform.position;
            if (Vector2.Distance(p, b) > 4f) continue;
            if (DistToSegment(p, a, b) < screenRadius) return true;
        }
        return false;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return Vector2.Distance(p, a + ab * t);
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        Ball ball = c.collider.GetComponent<Ball>();
        if (ball == null) return;
        Deflect(ball, c.relativeVelocity.magnitude);
    }

    // Rebote vivo: hacia fuera, con un ángulo que depende de dónde golpea y con parte de la velocidad
    void Deflect(Ball ball, float incomingSpeed)
    {
        float offset = (ball.rb.position.y - rb.position.y) / Mathf.Max(col.bounds.extents.y, 0.01f);
        float angleDeg = Mathf.Clamp(offset, -1f, 1f) * maxDeflectAngle + Random.Range(-10f, 10f);

        float side = Mathf.Abs(angleDeg) > 0.01f ? Mathf.Sign(angleDeg) : (Random.value < 0.5f ? -1f : 1f);
        if (qte != null && qte.State == QTEState.Success)
            angleDeg = side * Random.Range(qteClearAngle, 80f);          // parada del QTE: a la banda
        else if (Mathf.Abs(angleDeg) < minReboundAngle)
            angleDeg = side * minReboundAngle;                            // nunca recto al centro

        float angle = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle) * outwardX, Mathf.Sin(angle));

        float speedOut = Mathf.Max(minRebound, incomingSpeed * reboundFactor);
        ball.rb.linearVelocity = dir * speedOut;
        ball.Touch(team);

        tracking = false;
        SaveVisual();
    }

    void SaveVisual()
    {
        if (sr == null) return;
        sr.color = Color.yellow;
        flashUntil = Time.time + saveFlashTime;
    }

    public void ResetPosition()
    {
        EnsureFit();
        rb.position = startPos;
        tracking = false;
        if (qte != null) qte.Cancel();
    }

    // Cambio de campo: se pasa al lado contrario y se vuelve a medir con la portería de allí
    public void SwapSide()
    {
        startPos = new Vector2(-startPos.x, startPos.y);
        outwardX = -outwardX;
        fitted = false;
        EnsureFit();
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.cyan;
        float lim = LateralLimit;
        Gizmos.DrawLine(new Vector3(startPos.x, goalCenterY - lim), new Vector3(startPos.x, goalCenterY + lim));
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(lineX, goalCenterY - goalHalf), new Vector3(lineX, goalCenterY + goalHalf));
    }
}