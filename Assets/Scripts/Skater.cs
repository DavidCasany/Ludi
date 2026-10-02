using System.Collections;
using UnityEngine;

// El "cuerpo" del jugador. No lee input: otro script (TeamManager para el humano,
// SkaterAI para la IA) le dice hacia dónde moverse y a dónde apuntar.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Skater : MonoBehaviour
{
    [Header("Equipo")]
    public Team team;

    [Header("Movimiento (patinaje con inercia)")]
    public float maxSpeed = 7f;
    public float acceleration = 25f;
    public float deceleration = 6f;

    [Header("Bola")]
    public Transform holdPoint;          // opcional
    public float holdDistance = 0.8f;
    public float pickupRadius = 0.9f;
    public LayerMask ballLayer;
    public float shootForce = 14f;
    [Range(0f, 1f)] public float inheritVelocity = 0.5f;
    public float pickupCooldown = 0.4f;

    [Header("Robo de bola (stick check)")]
    public float stealCooldown = 0.7f;
    [Range(0f, 1f)] public float stealChance = 0.35f;

    [Header("Indicador de control (opcional)")]
    public GameObject controlIndicator;  // p. ej. un aro o flecha hijo del jugador

    // Lo rellena quien controla al jugador
    public Vector2 MoveInput { get; set; }
    public Vector2 AimDir { get; set; } = Vector2.right;

    public Vector2 Anchor { get; private set; }   // posición inicial = posición en la formación
    public Vector2 Velocity => rb.linearVelocity;
    public bool HasBall => ball != null;
    public bool IsControlled { get; private set; }

    Rigidbody2D rb;
    Collider2D col;
    Ball ball;
    float nextPickupTime;
    float nextStealTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        Anchor = transform.position;
        if (controlIndicator != null) controlIndicator.SetActive(false);
    }

    void Update()
    {
        if (holdPoint != null)
            holdPoint.position = (Vector2)transform.position + AimDir * holdDistance;
    }

    void FixedUpdate()
    {
        Move();

        if (ball == null) TryPickup();
        else ball.rb.position = HoldPosition();
    }

    // ---------- Control ----------

    public void SetControlled(bool value)
    {
        IsControlled = value;
        if (controlIndicator != null) controlIndicator.SetActive(value);

        // Si lo controla el humano, la IA se apaga; si no, se enciende
        SkaterAI ai = GetComponent<SkaterAI>();
        if (ai != null) ai.enabled = !value;
    }

    // ---------- Movimiento ----------

    void Move()
    {
        Vector2 input = Vector2.ClampMagnitude(MoveInput, 1f);
        Vector2 target = input * maxSpeed;
        float rate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
        // En Unity 2022 o anterior: rb.velocity
    }

    // ---------- Bola ----------

    Vector2 HoldPosition()
    {
        return holdPoint != null ? (Vector2)holdPoint.position
                                 : (Vector2)transform.position + AimDir * holdDistance;
    }

    void TryPickup()
    {
        if (Time.time < nextPickupTime) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, pickupRadius, ballLayer);
        if (hit == null) return;

        Ball b = hit.GetComponent<Ball>();
        if (b == null) return;

        if (b.IsHeld)
        {
            // Bola de un rival: intento de robo con el stick
            if (b.Holder.team == team || Time.time < nextStealTime) return;

            nextStealTime = Time.time + stealCooldown;
            if (Random.value < stealChance)
            {
                Vector2 push = ((Vector2)b.transform.position - (Vector2)transform.position).normalized;
                b.Holder.LoseBall(push);
            }
            return;
        }

        ball = b;
        ball.Grab(this);
        Physics2D.IgnoreCollision(col, ball.col, true); // no choca con quien la lleva
    }

    public void Shoot(Vector2 dir, float force)
    {
        if (!HasBall) return;
        DropBall(dir.normalized * force + rb.linearVelocity * inheritVelocity, pickupCooldown);
    }

    // Pase a un compañero: calcula la fuerza para que la bola llegue justo hasta él
    public void PassTo(Skater mate)
    {
        if (!HasBall || mate == null) return;

        Vector2 targetPos = (Vector2)mate.transform.position + mate.Velocity * 0.4f; // anticipa su movimiento
        Vector2 dir = targetPos - (Vector2)transform.position;

        // Con rozamiento exponencial: distancia = (v0 - vLlegada) / damping
        const float arriveSpeed = 3f;
        float speed = dir.magnitude * ball.rb.linearDamping + arriveSpeed;
        speed = Mathf.Clamp(speed, 5f, shootForce);

        DropBall(dir.normalized * speed, pickupCooldown);
    }

    // Te quitan la bola
    public void LoseBall(Vector2 pushDir)
    {
        if (!HasBall) return;
        DropBall(pushDir * 4f, 0.6f);
    }

    void DropBall(Vector2 velocity, float cooldown)
    {
        Ball b = ball;
        ball = null;
        nextPickupTime = Time.time + cooldown;
        b.Release(velocity);
        StartCoroutine(ReenableCollision(b, cooldown));
    }

    IEnumerator ReenableCollision(Ball b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics2D.IgnoreCollision(col, b.col, false);
    }

    // Tras un gol: volver a la formación
    public void ResetToAnchor()
    {
        if (ball != null)
        {
            Physics2D.IgnoreCollision(col, ball.col, false);
            ball = null;
        }
        rb.position = Anchor;
        rb.linearVelocity = Vector2.zero;
        MoveInput = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
