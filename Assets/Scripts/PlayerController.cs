using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento (patinaje con inercia)")]
    public float maxSpeed = 7f;
    public float acceleration = 25f;   // lo rápido que coge velocidad
    public float deceleration = 6f;    // lo que "resbala" al soltar las teclas

    [Header("Bola")]
    public Transform holdPoint;        // Empty hijo del jugador (opcional, se mueve solo)
    public float holdDistance = 0.8f;  // distancia del punto de agarre al jugador
    public float pickupRadius = 0.9f;  // radio para coger la bola
    public LayerMask ballLayer;        // layer "Ball"
    public float shootForce = 14f;
    [Range(0f, 1f)] public float inheritVelocity = 0.5f; // cuánto suma la velocidad del jugador al tiro
    public float pickupCooldown = 0.4f; // tiempo sin poder recoger tras chutar

    Rigidbody2D rb;
    Collider2D col;
    Camera cam;
    Ball ball;                          // bola que llevamos (null si no tenemos)
    Vector2 input;
    Vector2 aimDir = Vector2.right;
    float nextPickupTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        cam = Camera.main;
    }

    void Update()
    {
        // Input
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        // Apuntado hacia el ratón
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mouseWorld - (Vector2)transform.position;
        if (toMouse.sqrMagnitude > 0.001f) aimDir = toMouse.normalized;

        if (holdPoint != null)
            holdPoint.position = (Vector2)transform.position + aimDir * holdDistance;

        // Disparo
        if (Input.GetMouseButtonDown(0) && ball != null)
            Shoot();
    }

    void FixedUpdate()
    {
        Move();

        if (ball == null) TryPickup();
        else ball.rb.position = HoldPosition(); // la bola sigue al punto de agarre
    }

    void Move()
    {
        Vector2 target = input * maxSpeed;
        float rate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * Time.fixedDeltaTime);
        // En Unity 2022 o anterior usa rb.velocity en lugar de rb.linearVelocity
    }

    Vector2 HoldPosition()
    {
        return holdPoint != null ? (Vector2)holdPoint.position
                                 : (Vector2)transform.position + aimDir * holdDistance;
    }

    void TryPickup()
    {
        if (Time.time < nextPickupTime) return;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, pickupRadius, ballLayer);
        if (hit == null) return;

        Ball b = hit.GetComponent<Ball>();
        if (b == null || b.IsHeld) return;

        ball = b;
        ball.Grab();
        Physics2D.IgnoreCollision(col, ball.col, true); // que no choque contra nosotros mientras la llevamos
    }

    void Shoot()
    {
        Ball b = ball;
        ball = null;
        nextPickupTime = Time.time + pickupCooldown;

        b.Release(aimDir * shootForce + rb.linearVelocity * inheritVelocity);

        // Reactivamos la colisión con el jugador un momento después
        StartCoroutine(ReenableCollision(b));
    }

    System.Collections.IEnumerator ReenableCollision(Ball b)
    {
        yield return new WaitForSeconds(pickupCooldown);
        Physics2D.IgnoreCollision(col, b.col, false);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}