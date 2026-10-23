using UnityEngine;

// Rigidbody2D en modo Kinematic + un BoxCollider2D (no trigger). Colócalo CENTRADO sobre la línea de gol.
//
// Portero "humano" en vez de imán:
//  1) COLOCACIÓN: se sitúa sobre la bisectriz del ángulo bola-postes y sale un poco hacia la bola.
//     Con ángulo cerrado se pega al primer palo (adiós al hueco palo-portero desde la línea de fondo).
//     Recolocarse es LENTO (positioningSpeedFactor): un pase cruzado le pilla a contrapié.
//  2) TIRO: al detectar un tiro tarda 'reactionTime' en reaccionar (más si un rival le tapa la visión)
//     y después se estira a 'speed' hacia donde lee que cruzará la bola (con un pequeño error de lectura).
//  3) REBOTE: la bola sale con parte de su velocidad, no como un despeje perfecto: hay segundas jugadas.
//
// 'speed' y 'reach' los modifican las mejoras del torneo (TeamStatApplier): conservan su nombre.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Goalkeeper : MonoBehaviour
{
    public Team team;

    [Header("Movimiento (lo modifican las mejoras)")]
    [Tooltip("Velocidad de estirada lateral al reaccionar a un tiro (m/s)")]
    public float speed = 5.5f;
    [Tooltip("Desplazamiento lateral máximo del centro del portero respecto al centro de la portería (m)")]
    public float reach = 0.55f;

    [Header("Colocación")]
    [SerializeField] float goalHalfWidth = 0.85f;                       // igual que en TeamBrain
    [SerializeField, Range(0.2f, 1f)] float positioningSpeedFactor = 0.5f; // recolocarse = speed x esto
    [SerializeField] float maxDepth = 0.45f;                            // cuánto sale de la línea hacia la bola
    [SerializeField] float postHugInset = 0.2f;                         // con ángulo cerrado, distancia al palo

    [Header("Reacción al tiro")]
    [SerializeField] float reactionTime = 0.17f;
    [SerializeField] float screenPenalty = 0.1f;          // reacción extra si un rival se cruza en la línea de tiro
    [SerializeField] float screenRadius = 0.7f;
    [SerializeField] float shotMinSpeed = 9f;             // por debajo no se considera tiro
    [SerializeField, Range(0f, 0.5f)] float readError = 0.12f;   // error al leer por dónde cruzará (m)

    [Header("Parada y rebote")]
    [SerializeField, Range(0f, 1f)] float reboundFactor = 0.45f;  // velocidad del rebote = velocidad del tiro x esto
    [SerializeField] float minRebound = 4f;
    [SerializeField] float maxDeflectAngle = 60f;
    [SerializeField] float saveFlashTime = 0.25f;

    public float ReactionTime => reactionTime;
    public float HalfWidth => col != null ? col.bounds.extents.y : 0.4f;   // lo usa TeamBrain para valorar tiros

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    Color baseColor;
    float flashUntil;
    Vector2 startPos;   // centro de la portería sobre la línea de gol
    float outwardX;     // +1 o -1: hacia el centro de la pista (se asume el centro en x = 0)

    bool tracking;
    float shotTime;
    float shotDelay;
    float readOffset;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        startPos = rb.position;
        outwardX = Mathf.Sign(-startPos.x);
    }

    // Color del equipo (lo usa el torneo)
    public void SetColor(Color c)
    {
        baseColor = c;
        if (sr != null) sr.color = c;
    }

    void FixedUpdate()
    {
        Ball ball = Ball.Instance;
        Vector2 target = startPos;
        float moveSpeed = speed * positioningSpeedFactor;

        if (ball != null)
        {
            float crossY;
            if (IsShotIncoming(ball, out crossY))
            {
                if (!tracking)
                {
                    tracking = true;
                    shotTime = Time.time;
                    shotDelay = reactionTime + (IsScreened(ball, crossY) ? screenPenalty : 0f);
                    readOffset = Random.Range(-readError, readError);
                }

                if (Time.time - shotTime < shotDelay)
                {
                    target = rb.position;                                    // "plantado": aún no ha reaccionado
                }
                else
                {
                    target = new Vector2(rb.position.x, crossY + readOffset); // estirada lateral
                    moveSpeed = speed;
                }
            }
            else
            {
                tracking = false;
                target = PositionTarget(ball.rb.position);
            }
        }

        // Límites: lateral = reach, profundidad = entre la línea y maxDepth hacia fuera
        target.y = Mathf.Clamp(target.y, startPos.y - reach, startPos.y + reach);
        float minX = Mathf.Min(startPos.x, startPos.x + outwardX * maxDepth);
        float maxX = Mathf.Max(startPos.x, startPos.x + outwardX * maxDepth);
        target.x = Mathf.Clamp(target.x, minX, maxX);

        rb.MovePosition(Vector2.MoveTowards(rb.position, target, moveSpeed * Time.fixedDeltaTime));

        if (sr != null && Time.time > flashUntil) sr.color = baseColor;
    }

    // Bisectriz del ángulo bola-palos. Por el teorema de la bisectriz, corta la línea de gol en
    // Y = (d2·P1 + d1·P2) / (d1 + d2), con d1 = |bola-P1| y d2 = |bola-P2|.
    Vector2 PositionTarget(Vector2 bp)
    {
        float along = (bp.x - startPos.x) * outwardX;   // distancia de la bola por delante de la línea

        // Bola casi en la línea de fondo o detrás: pegado al primer palo
        if (along < 0.5f)
        {
            float side = Mathf.Sign(bp.y - startPos.y);
            return new Vector2(startPos.x, startPos.y + side * (goalHalfWidth - postHugInset));
        }

        Vector2 p1 = new Vector2(startPos.x, startPos.y - goalHalfWidth);
        Vector2 p2 = new Vector2(startPos.x, startPos.y + goalHalfWidth);
        float d1 = Vector2.Distance(bp, p1);
        float d2 = Vector2.Distance(bp, p2);
        Vector2 y = (d2 * p1 + d1 * p2) / (d1 + d2);

        // Sale hacia la bola (reduce el ángulo), menos cuanto más cerrado es el ángulo
        Vector2 dir = (bp - y).normalized;
        float depth = maxDepth * Mathf.Clamp01(Mathf.Abs(dir.x));
        return y + dir * depth;
    }

    // ¿Viene un tiro hacia la portería? Devuelve la Y por la que cruzará la línea de gol.
    bool IsShotIncoming(Ball ball, out float crossY)
    {
        crossY = startPos.y;
        if (ball.IsHeld) return false;

        Vector2 bp = ball.rb.position;
        Vector2 bv = ball.rb.linearVelocity;

        if (bv.sqrMagnitude < shotMinSpeed * shotMinSpeed) return false;
        if (bv.x * outwardX > -0.5f) return false;                 // no se acerca a la portería
        if ((bp.x - startPos.x) * outwardX < 0f) return false;     // ya está detrás de la línea

        float t = (startPos.x - bp.x) / bv.x;
        crossY = bp.y + bv.y * t;
        return Mathf.Abs(crossY - startPos.y) < goalHalfWidth + 0.5f;
    }

    // Un rival cruzado en la línea de tiro, cerca del portero, le tapa la visión
    bool IsScreened(Ball ball, float crossY)
    {
        Vector2 a = ball.rb.position;
        Vector2 b = new Vector2(startPos.x, crossY);

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

        // Rebote vivo: hacia fuera, con un ángulo que depende de dónde golpea, y con parte de la velocidad
        float offset = (ball.rb.position.y - rb.position.y) / Mathf.Max(col.bounds.extents.y, 0.01f);
        float angle = (Mathf.Clamp(offset, -1f, 1f) * maxDeflectAngle + Random.Range(-10f, 10f)) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle) * outwardX, Mathf.Sin(angle));

        float speedOut = Mathf.Max(minRebound, c.relativeVelocity.magnitude * reboundFactor);
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
        rb.position = startPos;
        tracking = false;
    }

    public void SwapSide()
    {
        startPos.x = -startPos.x;
        outwardX = -outwardX;
    }
}