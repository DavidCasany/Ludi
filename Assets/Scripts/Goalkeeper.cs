using UnityEngine;

// Rigidbody2D en modo Kinematic + un BoxCollider2D (no trigger).
// La posición se ajusta SOLA a la portería real (Goal): línea de gol, centro y anchura.
// Así funciona aunque RinkResizer cambie el tamaño de la pista o de las porterías.
//
//  1) COLOCACIÓN (como un portero de hockey patines): se mueve dentro de un ÁREA semicircular delante de la
//     portería, no en una línea. Se coloca sobre la bisectriz del ángulo bola-palos y SALE hacia la bola
//     (cierra el ángulo): mucho con la bola lejos y de frente, poco con la bola cerca o en ángulo cerrado.
//  2) TIRO: tarda 'reactionTime' en reaccionar (más si le tapan) y se estira a 'speed' hacia el punto de la
//     trayectoria de la bola más cercano a él (dentro de su área).
//     Portero del HUMANO (con GoalkeeperQTE): cada TIRO rival abre un QTE. Acertar = parada segura.
//     Los desvíos, bloqueos, rebotes y bolas lentas no abren QTE: el portero reacciona solo, como el de la IA.
//     Mientras se decide el QTE la bola NO puede entrar: si llega a la línea se congela hasta el resultado.
//  3) REBOTE: la bola sale con parte de su velocidad y nunca recta al centro: hay segundas jugadas.
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

    [Header("Área del portero (semicírculo delante de la portería)")]
    [Tooltip("Cuánto puede salir el portero desde la línea de gol hacia la pista (m)")]
    [SerializeField] float areaDepth = 1.3f;
    [Tooltip("Con la bola a esta distancia o más (y de frente) sale al máximo del área")]
    [SerializeField] float farBallDistance = 10f;
    [Tooltip("Con la bola a esta distancia o menos se queda pegado a la línea")]
    [SerializeField] float nearBallDistance = 3f;
    [SerializeField] float fallbackGoalHalfWidth = 0.85f; // solo si no encuentra ningún Goal

    [Header("Reacción al tiro")]
    [SerializeField] float reactionTime = 0.17f;
    [SerializeField] float screenPenalty = 0.1f;          // reacción extra si un rival se cruza en la línea de tiro
    [SerializeField] float screenRadius = 0.7f;
    [SerializeField] float shotMinSpeed = 6f;             // por debajo no se considera que la bola vaya a puerta
    [SerializeField, Range(0f, 0.5f)] float readError = 0.12f;   // error al leer por dónde cruzará (m)

    [Header("Parada y rebote")]
    [SerializeField, Range(0f, 1f)] float reboundFactor = 0.45f;
    [SerializeField] float minRebound = 4f;
    [SerializeField] float maxDeflectAngle = 60f;
    [SerializeField] float minReboundAngle = 25f;         // ningún rebote sale recto hacia la zona de remate
    [SerializeField] float saveFlashTime = 0.25f;

    [Header("QTE (solo portero del humano)")]
    [Tooltip("Solo los tiros más rápidos que esto abren un QTE (los lentos los para el portero solo)")]
    [SerializeField] float qteMinSpeed = 12f;
    [Tooltip("Más lejos de esto no hay QTE")]
    [SerializeField] float qteMaxDistance = 14f;
    [SerializeField] float qteClearAngle = 65f;           // parada del QTE: despeje hacia la banda (entre esto y 80°)
    [SerializeField] float qteFailExtraDelay = 0.08f;     // QTE fallado: reacciona algo más tarde que la IA
    [SerializeField] float qteFailReadMultiplier = 2.5f;  // ... y lee peor por dónde va la bola

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
    float minU;          // profundidad mínima del centro del portero respecto a la línea de gol
    float outwardX;      // +1 o -1: hacia el centro de la pista

    bool tracking;
    float shotTime;
    float shotDelay;
    float readOffset;
    int lastQteShot = -1;   // ShotId del último tiro que abrió un QTE (un QTE por tiro)

    // Seguro del QTE: si la bola llega a la línea mientras se decide, se queda congelada ahí hasta el resultado
    bool holdingBall;
    Vector2 heldVelocity;

    // Desplazamiento lateral máximo del centro: cuerpo tocando el palo + reach
    float LateralLimit => Mathf.Max(0f, goalHalf - HalfWidth) + reach;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
        qte = GetComponent<GoalkeeperQTE>();
        owner = GetComponentInParent<TeamManager>();

        startPos = rb.position;
        outwardX = Mathf.Sign(-startPos.x);
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
            minU = 0f;
            return;
        }

        Vector2 mouth = g.Mouth;
        outwardX = mouth.x > 0f ? -1f : 1f;
        lineX = mouth.x;
        goalCenterY = mouth.y;
        goalHalf = g.HalfWidth;

        float halfDepth = col.bounds.extents.x;
        minU = halfDepth + lineOffset;
        startPos = new Vector2(lineX + outwardX * minU, goalCenterY);
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

        if (ball != null && holdingBall)
        {
            if (ResolveHeldBall(ball)) return;
        }

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
                }

                // Un QTE por TIRO (no por "bola que viene"): aunque el anterior acabe de resolverse, un tiro nuevo abre otro
                if (ball.ShotId != lastQteShot && UseQTE(ball, timeToLine))
                {
                    lastQteShot = ball.ShotId;
                    shotTime = Time.time;   // la reacción cuenta desde este tiro
                    qte.Begin(Vector2.Distance(ball.rb.position, startPos), timeToLine);
                }

                QTEState q = qte != null ? qte.State : QTEState.Idle;

                if (q == QTEState.Running)
                {
                    target = rb.position;                                   // decidiendo (el tiempo va casi parado)

                    // Seguro: mientras se decide el QTE la bola no puede entrar. Si llega a la línea, se congela ahí.
                    if (DepthOf(ball.rb.position) < ball.col.bounds.extents.x + 0.05f)
                    {
                        holdingBall = true;
                        heldVelocity = ball.rb.linearVelocity;
                        ball.rb.linearVelocity = Vector2.zero;
                    }
                }
                else if (q == QTEState.Success)
                {
                    // Parada garantizada: se coloca al instante y, cuando la bola llega a su altura, la despeja él
                    Vector2 savePos = InterceptPoint(ball);
                    rb.position = savePos;
                    target = savePos;

                    if (DepthOf(ball.rb.position) - DepthOf(rb.position) < col.bounds.extents.x + 0.6f)
                    {
                        Deflect(ball, ball.Speed);
                        return;
                    }
                }
                else
                {
                    // Portero de la IA, bola que no es un tiro, o QTE fallado: reacción normal
                    // (si ha fallado el QTE, reacciona más tarde y lee peor por dónde va la bola)
                    bool failed = q == QTEState.Fail;
                    float delay = shotDelay + (failed ? qteFailExtraDelay : 0f);
                    float error = readOffset * (failed ? qteFailReadMultiplier : 1f);

                    if (Time.time - shotTime < delay)
                    {
                        target = rb.position;
                    }
                    else
                    {
                        target = InterceptPoint(ball) + new Vector2(0f, error);
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

        // Límites: siempre dentro de su área
        target = ClampToArea(target);

        rb.MovePosition(Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime));

        if (sr != null && Time.time > flashUntil) sr.color = baseColor;
    }

    // Bola congelada en la línea esperando el QTE. Devuelve true si este frame ya está resuelto.
    bool ResolveHeldBall(Ball ball)
    {
        QTEState q = qte != null ? qte.State : QTEState.Idle;

        if (q == QTEState.Running && !ball.IsHeld)
        {
            ball.rb.linearVelocity = Vector2.zero;   // sigue esperando
            return true;
        }

        holdingBall = false;
        if (ball.IsHeld) return false;               // alguien la ha cogido: se acabó el seguro

        if (q == QTEState.Success)
        {
            // Parada sobre la línea: el portero llega a la bola y la despeja
            rb.position = ClampToArea(new Vector2(rb.position.x, ball.rb.position.y));
            Deflect(ball, heldVelocity.magnitude);
            return true;
        }

        ball.rb.linearVelocity = heldVelocity;        // fallado (o cancelado): la bola sigue su camino
        return false;
    }

    // ---------- Área ----------
    // Coordenadas locales: u = profundidad desde la línea de gol hacia la pista, v = lateral desde el centro.
    float DepthOf(Vector2 p) => (p.x - lineX) * outwardX;
    Vector2 FromLocal(float u, float v) => new Vector2(lineX + outwardX * u, goalCenterY + v);

    // El área es medio óvalo apoyado en la línea: ancho = de palo a palo (+ reach), fondo = areaDepth.
    // Pegado a la línea puede llegar a los palos; cuanto más sale, menos se puede abrir hacia los lados.
    Vector2 ClampToArea(Vector2 p)
    {
        float u = DepthOf(p);
        float v = p.y - goalCenterY;
        float depthRange = Mathf.Max(0.01f, areaDepth - minU);
        float B = Mathf.Max(0.01f, LateralLimit);

        float du = Mathf.Max(0f, u - minU);
        float k = (du * du) / (depthRange * depthRange) + (v * v) / (B * B);
        if (k > 1f)
        {
            float s = 1f / Mathf.Sqrt(k);
            du *= s;
            v *= s;
        }
        return FromLocal(minU + du, Mathf.Clamp(v, -B, B));
    }

    // Punto de la trayectoria de la bola más cercano al portero (dentro de su área): hacia ahí se estira
    Vector2 InterceptPoint(Ball ball)
    {
        Vector2 bp = ball.rb.position;
        Vector2 d = ball.rb.linearVelocity.normalized;
        float t = Mathf.Max(0f, Vector2.Dot(rb.position - bp, d));
        return ClampToArea(bp + d * t);
    }

    // Colocación sin tiro: sobre la bisectriz del ángulo bola-palos, saliendo hacia la bola para cerrar el ángulo.
    // La bisectriz corta la línea de gol en Y = (d2·P1 + d1·P2) / (d1 + d2).
    Vector2 PositionTarget(Vector2 bp)
    {
        float along = DepthOf(bp);   // distancia de la bola por delante de la línea

        // Bola casi en la línea de fondo o detrás: pegado al primer palo
        if (along < 0.5f)
        {
            float side = Mathf.Sign(bp.y - goalCenterY);
            return FromLocal(minU, side * (goalHalf - HalfWidth * 0.9f));
        }

        Vector2 p1 = new Vector2(lineX, goalCenterY - goalHalf);
        Vector2 p2 = new Vector2(lineX, goalCenterY + goalHalf);
        float d1 = Vector2.Distance(bp, p1);
        float d2 = Vector2.Distance(bp, p2);
        Vector2 y = (d2 * p1 + d1 * p2) / (d1 + d2);

        Vector2 dir = (bp - y).normalized;
        float facing = Mathf.Clamp01(dir.x * outwardX);                     // 1 = bola de frente, 0 = en la línea de fondo
        float far01 = Mathf.InverseLerp(nearBallDistance, farBallDistance, Vector2.Distance(bp, y));
        float depth = Mathf.Lerp(minU, areaDepth, far01 * facing);          // lejos y de frente: sale más

        float t = facing > 0.01f ? depth / facing : 0f;                     // punto de la bisectriz a esa profundidad
        return ClampToArea(y + dir * t);
    }

    // ¿Viene la bola a PUERTA? Devuelve por dónde cruzará la línea del portero y cuánto le falta.
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

    // QTE solo en el portero del HUMANO y solo con TIROS del rival:
    // no con pases, desvíos de stick, bloqueos, rebotes del portero ni bolas lentas.
    bool UseQTE(Ball ball, float timeToLine)
    {
        if (qte == null || !qte.enabled || !HumanKeeper) return false;
        if (!ball.IsShot || ball.ShotTeam == team) return false;
        if (ball.Speed < qteMinSpeed) return false;
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
        ball.Touch(team);   // la bola deja de ser "tiro": el rebote no abre otro QTE

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
        holdingBall = false;
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

        // Área del portero (cian) y boca de la portería (rojo)
        Gizmos.color = Color.cyan;
        Vector2 prev = FromLocal(minU, -LateralLimit);
        for (int i = 1; i <= 24; i++)
        {
            float a = Mathf.Lerp(-90f, 90f, i / 24f) * Mathf.Deg2Rad;
            Vector2 p = FromLocal(minU + Mathf.Cos(a) * (areaDepth - minU), Mathf.Sin(a) * LateralLimit);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(lineX, goalCenterY - goalHalf), new Vector3(lineX, goalCenterY + goalHalf));
    }
}