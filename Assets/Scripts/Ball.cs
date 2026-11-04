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

    // Tiros: cada tiro de un jugador tiene un número (ShotId). El portero del humano abre UN QTE por tiro.
    // Un pase, un toque de stick, un bloqueo o una parada dejan de ser "tiro" (IsShot = false).
    public bool IsShot { get; private set; }
    public Team ShotTeam { get; private set; }
    public int ShotId { get; private set; }
    public Skater Shooter { get; private set; }      // quién ha tirado (no puede desviar su propio tiro)
    public float ShotDistance { get; private set; }  // distancia del tiro a la portería atacada (tiro exterior o no)

    // Jugada: tiros seguidos del mismo equipo sin que el rival recupere la bola (los rebotes suman)
    public int ShotStreak { get; private set; }
    Team streakTeam;
    bool streakAlive;

    // Cada desvío (un jugador toca un tiro y le cambia la trayectoria) suma 1: el portero tiene que volver a reaccionar
    public int DeflectId { get; private set; }

    [Header("Pase guiado (asistencia)")]
    [Tooltip("Grados por segundo que la bola puede girar para corregir su trayectoria hacia el receptor")]
    public float guideTurnRate = 320f;
    [Tooltip("Si la desviación respecto al receptor es mayor que esto, la bola no se corrige (el pase salió mal a propósito)")]
    public float guideMaxAngle = 55f;
    public float guideDuration = 1.6f;

    [Header("Estela")]
    public bool showTrail = true;
    public float trailMinSpeed = 9f;
    [Tooltip("A aquesta velocitat l'estela és al màxim (llarga, ampla i taronja si és un xut)")]
    public float trailMaxSpeed = 30f;

    [Header("Hit stop dels xuts")]
    [Tooltip("Els xuts més ràpids que això congelen el joc un instant (HitStop)")]
    public float shotHitStopSpeed = 18f;
    public float shotHitStopMin = 0.03f, shotHitStopMax = 0.07f;

    // Estado del pase guiado
    Skater guideTarget;
    bool hasWaypoint;
    Vector2 waypoint;
    float guideUntil;

    TrailRenderer trail, trailCore;
    int hitStopShot = -1;
    float shotTime = -10f;

    void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (showTrail) CreateTrail();
    }

    void FixedUpdate()
    {
        CheckShotHitStop();
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
        IsShot = false;
        if (skater.team != streakTeam) streakAlive = false;   // el rival la recupera: se acaba la jugada
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
        IsShot = false;
    }

    // Lo llama Skater.Shoot: a partir de aquí la bola es un tiro de 'team'
    public void MarkShot(Team team, Skater shooter = null)
    {
        IsShot = true;
        ShotTeam = team;
        ShotId++;
        shotTime = Time.time;
        Shooter = shooter;
        passUntil = 0f;

        ShotStreak = streakAlive && streakTeam == team ? ShotStreak + 1 : 1;
        streakTeam = team;
        streakAlive = true;

        ShotDistance = float.MaxValue;
        for (int i = 0; i < Goal.All.Count; i++)
            if (Goal.All[i].defendingTeam != team)
                ShotDistance = Mathf.Min(ShotDistance, Vector2.Distance(rb.position, Goal.All[i].Mouth));
    }

    // Un jugador toca el tiro y le cambia la trayectoria: sigue siendo el mismo tiro (no abre otro QTE)
    public void Deflect(Team team)
    {
        LastTouchTeam = team;
        passUntil = 0f;
        guideTarget = null;
        DeflectId++;
    }

    // Para toques que no son de un Skater (p. ej. el portero)
    public void Touch(Team team)
    {
        LastTouchTeam = team;
        passUntil = 0f;
        guideTarget = null;
        IsShot = false;   // un desvío ya no es el tiro original
    }

    public void ResetTo(Vector2 position)
    {
        Holder = null;
        passUntil = 0f;
        guideTarget = null;
        IsShot = false;
        streakAlive = false;
        ShotStreak = 0;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = position;
        if (trail != null) trail.Clear();
        if (trailCore != null) trailCore.Clear();
    }

    // Xut potent: una congelació molt curta just quan surt la bola (una vegada per xut)
    void CheckShotHitStop()
    {
        if (!IsShot || ShotId == hitStopShot || Time.time - shotTime > 0.15f) return;
        float sp = Speed;
        if (sp < shotHitStopSpeed) return;
        hitStopShot = ShotId;
        float k = Mathf.InverseLerp(shotHitStopSpeed, trailMaxSpeed, sp);
        HitStop.Do(Mathf.Lerp(shotHitStopMin, shotHitStopMax, k));
    }

    // ---------- Estela ----------

    // Segons la velocitat: més llarga, més ampla i més opaca. Els xuts van de crema a taronja i porten
    // un nucli blanc; els passis, blanc blavós. Amb la bola agafada no n'hi ha.
    void Update()
    {
        if (trail == null) return;
        float sp = IsHeld ? 0f : Speed;
        bool on = sp > trailMinSpeed;
        float k = Mathf.InverseLerp(trailMinSpeed, trailMaxSpeed, sp);

        trail.emitting = on;
        if (on)
        {
            trail.time = Mathf.Lerp(0.12f, 0.3f, k);
            trail.widthMultiplier = Mathf.Lerp(0.75f, 1.2f, k);
            Color head = IsShot ? Color.Lerp(new Color(1f, 0.93f, 0.7f), new Color(1f, 0.55f, 0.15f), k)
                                : Color.Lerp(Color.white, new Color(0.72f, 0.9f, 1f), k);
            float a = Mathf.Lerp(0.35f, 0.85f, k);
            trail.startColor = new Color(head.r, head.g, head.b, a);
            trail.endColor = new Color(head.r, head.g, head.b, 0f);
        }

        if (trailCore != null)
        {
            bool core = on && IsShot && k > 0.35f;
            trailCore.emitting = core;
            if (core)
            {
                trailCore.time = Mathf.Lerp(0.06f, 0.14f, k);
                trailCore.startColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.4f, 0.95f, k));
            }
        }
    }

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
        trail.minVertexDistance = 0.05f;
        trail.emitting = false;

        // Nucli blanc i estret dels xuts forts (un TrailRenderer per GameObject: va en un fill)
        GameObject coreGo = new GameObject("TrailCore");
        coreGo.transform.SetParent(transform, false);
        trailCore = coreGo.AddComponent<TrailRenderer>();
        trailCore.time = 0.1f;
        trailCore.startWidth = 0.12f;
        trailCore.endWidth = 0f;
        trailCore.material = trail.material;
        trailCore.startColor = Color.white;
        trailCore.endColor = new Color(1f, 1f, 1f, 0f);
        trailCore.sortingOrder = 6;
        trailCore.minVertexDistance = 0.05f;
        trailCore.emitting = false;
    }
}