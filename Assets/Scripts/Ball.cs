using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Ball : MonoBehaviour
{
    public static Ball Instance { get; private set; }

    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Collider2D col;

    public Skater Holder { get; private set; }
    public bool IsHeld => Holder != null;
    public Team LastTouchTeam { get; private set; }
    public float Speed => rb.linearVelocity.magnitude;

    // Ventana de pase: tras un pase, los compañeros del pasador pueden controlar la bola aunque vaya rápida.
    // Los rivales no: solo la interceptan si va lenta o con un golpe de stick bien dado.
    public Team PassTeam { get; private set; }
    float passUntil;

    public bool IsPassFor(Team team) => !IsHeld && Time.time < passUntil && PassTeam == team;

    [Header("Pase guiado (asistencia)")]
    [Tooltip("Grados por segundo que la bola puede girar para corregir su trayectoria hacia el receptor")]
    public float guideTurnRate = 320f;
    [Tooltip("Si la desviación respecto al receptor es mayor que esto, la bola no se corrige (el pase salió mal a propósito)")]
    public float guideMaxAngle = 55f;
    public float guideDuration = 1.6f;

    [Header("Estela")]
    public bool showTrail = true;
    public float trailMinSpeed = 9f;

    // Estado del pase guiado
    Skater guideTarget;
    bool hasWaypoint;
    Vector2 waypoint;
    float guideUntil;

    TrailRenderer trail;

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (showTrail) CreateTrail();
    }

    void FixedUpdate()
    {
        if (trail != null) trail.emitting = Speed > trailMinSpeed;
        SteerGuidedPass();
    }

    // ---------- Pase guiado ----------

    // Un pase a un compañero no depende solo de la física: la bola corrige su rumbo hacia el receptor.
    // Si 'wp' tiene valor, primero va hacia ese punto de la banda y, tras rebotar, hacia el receptor.
    public void SetGuide(Skater target, Vector2? wp)
    {
        guideTarget = target;
        hasWaypoint = wp.HasValue;
        waypoint = wp.HasValue ? wp.Value : Vector2.zero;
        guideUntil = Time.time + guideDuration;
    }

    public void ClearGuide() { guideTarget = null; }

    void SteerGuidedPass()
    {
        if (guideTarget == null) return;
        if (IsHeld || Time.time > guideUntil) { guideTarget = null; return; }

        Vector2 pos = rb.position;
        Vector2 v = rb.linearVelocity;
        float sp = v.magnitude;
        if (sp < 2f) { guideTarget = null; return; }

        Vector2 aim = Vector2.zero;

        if (hasWaypoint)
        {
            aim = waypoint;
            if (Vector2.Distance(pos, waypoint) < 1f) hasWaypoint = false;   // ya está en la banda
        }

        if (!hasWaypoint)
        {
            Vector2 tp = guideTarget.transform.position;
            float dist = Vector2.Distance(pos, tp);
            if (dist < 1.2f) { guideTarget = null; return; }                 // ya llega: que lo recoja el receptor

            float lead = Mathf.Min(dist / Mathf.Max(sp, 1f), 0.6f);          // anticipa su movimiento
            aim = tp + guideTarget.Velocity * lead;
        }

        float ang = Vector2.SignedAngle(v, aim - pos);
        if (Mathf.Abs(ang) > guideMaxAngle)
        {
            if (!hasWaypoint) guideTarget = null;
            return;
        }

        float maxStep = guideTurnRate * Time.fixedDeltaTime;
        rb.linearVelocity = Rotate(v, Mathf.Clamp(ang, -maxStep, maxStep));
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (guideTarget == null) return;

        // Choca con un jugador: la asistencia termina (la física decide)
        if (c.collider.GetComponent<Skater>() != null) { guideTarget = null; return; }

        // Choca con la banda: si iba hacia un rebote, ahora va hacia el receptor
        if (hasWaypoint) hasWaypoint = false;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ---------- Posesión ----------

    public void Grab(Skater skater)
    {
        Holder = skater;
        LastTouchTeam = skater.team;
        passUntil = 0f;
        guideTarget = null;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (trail != null) trail.Clear();
    }

    public void Release(Vector2 velocity)
    {
        Holder = null;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = velocity;
    }

    public void MarkPass(Team team, float duration)
    {
        PassTeam = team;
        passUntil = Time.time + duration;
    }

    // Para toques que no son de un Skater (p. ej. el portero)
    public void Touch(Team team)
    {
        LastTouchTeam = team;
        passUntil = 0f;
        guideTarget = null;
    }

    public void ResetTo(Vector2 position)
    {
        Holder = null;
        passUntil = 0f;
        guideTarget = null;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
        if (trail != null) trail.Clear();
    }

    // ---------- Estela ----------

    void CreateTrail()
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.22f;
        trail.startWidth = 0.28f;
        trail.endWidth = 0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 1f, 1f, 0.55f);
        trail.endColor = new Color(1f, 1f, 1f, 0f);
        trail.sortingOrder = 5;
        trail.emitting = false;
    }
}
