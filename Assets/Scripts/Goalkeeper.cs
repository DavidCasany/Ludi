using UnityEngine;

// Rigidbody2D en modo Kinematic + un BoxCollider2D (no trigger).
// Se mueve en vertical sobre la línea de gol y rechaza la bola hacia el centro de la pista.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Goalkeeper : MonoBehaviour
{
    public Team team;

    [Header("Movimiento")]
    public float speed = 6f;
    public float reach = 1.4f;                       // recorrido máximo arriba/abajo desde su posición inicial
    [Range(0f, 1f)] public float anticipation = 0.7f; // 0 = solo sigue la bola, 1 = predice la trayectoria

    [Header("Parada")]
    public float clearSpeed = 11f;                   // velocidad mínima del rechace
    public float maxDeflectAngle = 50f;              // según dónde golpea la bola, el rechace sale más abierto
    public float saveFlashTime = 0.25f;

    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    Color baseColor;
    float flashUntil;
    Vector2 startPos;
    float outwardX; // +1 o -1: hacia el centro de la pista (se asume el centro en x = 0)

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;

        startPos = rb.position;
        outwardX = Mathf.Sign(-startPos.x);
    }

    void FixedUpdate()
    {
        Ball ball = Ball.Instance;
        float targetY = startPos.y;

        if (ball != null)
        {
            Vector2 bp = ball.rb.position;
            Vector2 bv = ball.rb.linearVelocity;
            targetY = bp.y;

            // Si la bola viene hacia la portería, predecir por dónde cruzará la línea
            bool coming = Mathf.Abs(bv.x) > 0.5f && Mathf.Sign(bv.x) == -outwardX;
            if (coming)
            {
                float t = (startPos.x - bp.x) / bv.x;
                float predictedY = bp.y + bv.y * t;
                targetY = Mathf.Lerp(bp.y, predictedY, anticipation);
            }
        }

        targetY = Mathf.Clamp(targetY, startPos.y - reach, startPos.y + reach);
        float newY = Mathf.MoveTowards(rb.position.y, targetY, speed * Time.fixedDeltaTime);
        rb.MovePosition(new Vector2(startPos.x, newY));

        if (sr != null && Time.time > flashUntil) sr.color = baseColor;
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        Ball ball = c.collider.GetComponent<Ball>();
        if (ball == null) return;

        // Rechace hacia fuera; el ángulo depende de en qué parte del portero golpea
        float offset = (ball.rb.position.y - rb.position.y) / col.bounds.extents.y;
        float angle = Mathf.Clamp(offset, -1f, 1f) * maxDeflectAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle) * outwardX, Mathf.Sin(angle));

        float speedOut = Mathf.Max(clearSpeed, c.relativeVelocity.magnitude * 0.6f);
        ball.rb.linearVelocity = dir * speedOut;
        ball.Touch(team);

        SaveVisual();
    }

    // De momento solo parpadea. Aquí cambiaréis el sprite a la pose de "parada".
    void SaveVisual()
    {
        if (sr == null) return;
        sr.color = Color.yellow;
        flashUntil = Time.time + saveFlashTime;
    }

    public void ResetPosition()
    {
        rb.position = startPos;
    }
    public void SwapSide()
    {
        startPos.x = -startPos.x;
        outwardX = -outwardX;
    }
}
