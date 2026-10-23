using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StickResult { None, Cooldown, Whiff, Stolen, Intercepted, TooFast, Shielded, Dribbled, Grace, Failed }

// Un pase planificado: a qué compañero, y si va directo o rebotando en la banda
public struct PassPlan
{
    public Skater mate;
    public bool bank;          // true = rebota en la banda antes de llegar
    public Vector2 waypoint;   // directo: punto de llegada; con rebote: punto de la banda
    public float length;       // longitud total del recorrido
    public Vector2 firstDir;   // dirección de salida de la bola
}

// El "cuerpo" del jugador. No lee input: otro script (TeamManager para el humano,
// SkaterAI para la IA) le dice hacia dónde moverse, a dónde apuntar y qué acciones hacer.
//
// REGLAS DE LA BOLA
//  - Una bola SUELTA solo se queda automáticamente si va despacio (autoPickupMaxSpeed).
//    Los pases de un compañero se controlan hasta receiverMaxSpeed.
//  - La bola de un rival NO se roba por proximidad: se roba con el STICK (Stick()).
//
// ROBOS: dependen del ÁNGULO, no del azar puro.
//  - Entrar por el lado de la bola roba casi siempre (stealMaxChance); por el lateral es una apuesta
//    (stealBaseChance). Influyen también si el portador corre y los atributos (stealRating / ballControl).
//  - No se puede robar si el portador protege la bola con el cuerpo, está regateando o acaba de recibirla.
//  - Todo golpe que no consigue la bola deja al atacante descolocado (missRecovery), menos si el intento era bueno.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Skater : MonoBehaviour
{
    public static readonly List<Skater> All = new List<Skater>();

    [Header("Equipo")]
    public Team team;

    [Header("Movimiento (patinaje con inercia)")]
    public float maxSpeed = 7f;
    public float acceleration = 25f;
    public float deceleration = 6f;
    [Range(0.3f, 1.5f)] public float speedScale = 0.8f;   // escala global de velocidad y aceleración (bájala para un juego más lento)
    [Range(0f, 0.8f)] public float turnPenalty = 0.35f;   // aceleración que se pierde al girar de golpe (inercia de patín)

    [Header("Sprint")]
    public float sprintMultiplier = 1.4f;       // velocidad y aceleración extra
    public float sprintDuration = 2.5f;         // segundos de sprint con la barra llena
    public float sprintRecharge = 4f;           // segundos para recargar la barra completa
    public float sprintRechargeDelay = 0.6f;    // espera tras dejar de correr antes de recargar
    [Range(0f, 1f)] public float sprintResumeThreshold = 0.25f; // tras agotarla, hay que recuperar esto para volver a correr
    public float sprintHoldExtra = 0.25f;       // con bola y corriendo, la bola va más separada (más fácil de robar)

    [Header("Bola")]
    public Transform holdPoint;          // opcional
    public float holdDistance = 0.8f;
    public float pickupRadius = 0.9f;
    public LayerMask ballLayer;
    public LayerMask wallLayer;
    public float shootForce = 28f;       // velocidad del tiro
    [Range(0f, 1f)] public float inheritVelocity = 0.5f;
    public float pickupCooldown = 0.4f;

    [Header("Pases")]
    public float passSpeedPerMeter = 2.2f;  // velocidad = distancia x esto (≈ 0.5 s de vuelo)
    public float passSpeedMin = 9f;
    public float passSpeedMax = 24f;        // el tiro (shootForce) sigue siendo más rápido
    public float passWindow = 1.5f;         // tiempo en el que los compañeros pueden controlar el pase

    [Header("Asistencia de pase")]
    public float passAssistAngle = 14f;        // el pase se ajusta a un compañero si apuntas a menos de este ángulo
    public float laneBlockRadius = 1f;         // un rival a menos de esto de la línea de pase la bloquea
    public float bankWallMargin = 0.3f;        // separación de la banda donde rebota la bola
    public float bankCornerClearance = 3f;     // el rebote debe caer lejos de las esquinas redondeadas
    public float bankMaxLength = 40f;          // longitud máxima de un pase con rebote

    [Header("Control de la bola (recogida)")]
    public float autoPickupMaxSpeed = 7f;   // velocidad máxima para quedarse una bola suelta por proximidad
    public float receiverMaxSpeed = 24f;    // ... si es un pase de un compañero
    public float receiverPickupBonus = 1.3f;// el receptor de un pase tiene más radio de recogida

    [Header("Atributos (estilo 2K, 0 a 1)")]
    [Range(0f, 1f)] public float stealRating = 0.5f;   // habilidad para robar e interceptar
    [Range(0f, 1f)] public float ballControl = 0.5f;   // habilidad para proteger la bola

    [Header("Stick: alcance")]
    public float stickReach = 1.4f;                 // alcance del stick desde el centro del jugador
    [Range(20f, 180f)] public float stickArc = 110f;// ángulo (total) del arco frontal del stick
    public float stickCatchMaxSpeed = 18f;          // velocidad máxima de una bola suelta que se puede interceptar
    public float minSwingInterval = 0.2f;           // mínimo entre golpes (aunque no haya cooldown)

    [Header("Stick: robos")]
    public bool useStealCooldown = true;            // ¿hay cooldown entre intentos de robo/intercepción?
    public float stealCooldown = 1.5f;              // duración del cooldown (si useStealCooldown está activo)
    [Range(0f, 1f)] public float stealBaseChance = 0.2f;   // probabilidad entrando por el PEOR ángulo válido
    [Range(0f, 1f)] public float stealMaxChance = 0.8f;    // probabilidad entrando de cara a la bola
    [Range(0f, 0.5f)] public float sprintStealBonus = 0.12f;  // se suma si el portador corre con la bola
    [Range(0f, 1f)] public float interceptSuccessChance = 0.45f; // prob. de controlar un pase rápido con el stick
    public float missRecovery = 0.6f;               // tiempo descolocado tras un golpe fallido (el coste del riesgo)
    public float possessionGrace = 0.6f;            // tras recibir la bola no te la pueden robar
    [Range(-1f, 1f)] public float shieldDot = -0.2f;// la bola está protegida si el atacante queda por debajo de este valor
    public bool logSteals = false;                  // imprime en consola la probabilidad de cada robo

    [Header("Regate (juke)")]
    public float jukeSpeed = 13f;
    public float jukeDuration = 0.28f;
    public float jukeCooldown = 1.2f;
    [Range(0f, 1f)] public float jukeStaminaCost = 0.2f;
    public float jukeBeatRange = 2.5f;              // rivales a menos de esto quedan descolocados
    public float jukeBeatTime = 0.5f;
    [Range(0.1f, 1f)] public float staggerSlow = 0.4f; // velocidad de un jugador descolocado

    [Header("Indicador de control (opcional)")]
    public GameObject controlIndicator;  // p. ej. un aro o flecha hijo del jugador

    // Lo rellena quien controla al jugador
    public Vector2 MoveInput { get; set; }
    public bool SprintInput { get; set; }
    public Vector2 AimDir { get; set; } = Vector2.right;

    public Vector2 Anchor { get; private set; }   // posición inicial = posición en la formación
    public Vector2 Velocity => rb.linearVelocity;
    public bool HasBall => ball != null;
    public bool IsControlled { get; private set; }

    public float TopSpeed => maxSpeed * speedScale;   // velocidad máxima real (sin sprint)
    public bool IsSprinting { get; private set; }
    public bool IsExhausted => exhausted;
    public float Stamina01 => stamina;
    public bool IsDribbling => Time.time < jukeUntil;
    public bool IsStaggered => Time.time < staggerUntil;
    public bool IsProtected => IsDribbling || Time.time < graceUntil;   // no se le puede robar ahora mismo
    public float CurrentHoldDistance => holdDistance + (IsSprinting ? sprintHoldExtra : 0f);

    public float StickCooldown01 => (useStealCooldown && Time.time < nextStealTime)
        ? Mathf.Clamp01((nextStealTime - Time.time) / Mathf.Max(0.01f, stealCooldown)) : 0f;
    public float JukeCooldown01 => Time.time < nextJukeTime
        ? Mathf.Clamp01((nextJukeTime - Time.time) / Mathf.Max(0.01f, jukeCooldown)) : 0f;

    public bool CanStick => !HasBall && CanMove && !IsStaggered && Time.time >= nextSwingTime &&
                            (!useStealCooldown || Time.time >= nextStealTime);
    public bool CanJuke => HasBall && CanMove && !IsDribbling && !IsStaggered &&
                           Time.time >= nextJukeTime && stamina >= jukeStaminaCost;

    Rigidbody2D rb;
    Collider2D col;
    Ball ball;

    float nextPickupTime;
    float nextStealTime;
    float nextSwingTime;
    float graceUntil;
    float staggerUntil;
    float jukeUntil;
    float nextJukeTime;
    Vector2 jukeDir;

    float stamina = 1f;
    bool exhausted;
    float lastSprintTime = -10f;

    static readonly Color colGood = new Color(0.4f, 1f, 0.5f);
    static readonly Color colBad = new Color(1f, 0.4f, 0.4f);
    static readonly Color colInfo = new Color(0.5f, 0.85f, 1f);
    static readonly Color colWarn = new Color(1f, 0.9f, 0.3f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        Anchor = transform.position;
        if (controlIndicator != null) controlIndicator.SetActive(false);
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        if (holdPoint != null)
            holdPoint.position = (Vector2)transform.position + AimDir * CurrentHoldDistance;
    }

    void FixedUpdate()
    {
        if (!CanMove)
        {
            MoveInput = Vector2.zero;
            SprintInput = false;
            rb.linearVelocity = Vector2.zero;   // quieto de verdad, sin deslizar
        }

        Move();
        KeepOutOfGoals();

        if (ball == null) { if (CanMove) TryPickup(); }
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
        float dt = Time.fixedDeltaTime;

        // Regate: impulso rápido en una dirección fija
        if (IsDribbling)
        {
            rb.linearVelocity = jukeDir * jukeSpeed * speedScale;
            IsSprinting = false;
            return;
        }

        Vector2 input = Vector2.ClampMagnitude(MoveInput, 1f);
        bool moving = input.sqrMagnitude > 0.01f;

        UpdateSprint(moving, dt);

        float sprintMul = IsSprinting ? sprintMultiplier : 1f;
        float slowMul = IsStaggered ? staggerSlow : 1f;

        Vector2 target = input * TopSpeed * sprintMul * slowMul;

        float rate;
        if (moving)
        {
            rate = acceleration * speedScale * sprintMul;

            // Inercia de patín: girar de golpe cuesta aceleración
            Vector2 vel = rb.linearVelocity;
            if (turnPenalty > 0f && vel.sqrMagnitude > 1f)
            {
                float turn = Vector2.Angle(vel, input) / 180f;   // 0 = misma dirección, 1 = marcha atrás
                rate *= 1f - turnPenalty * turn;
            }
        }
        else
        {
            rate = deceleration * speedScale;
        }

        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, target, rate * dt);
        // En Unity 2022 o anterior: rb.velocity
    }

    void UpdateSprint(bool moving, float dt)
    {
        bool want = SprintInput && moving && !exhausted && stamina > 0f;
        IsSprinting = want;

        if (want)
        {
            stamina -= dt / Mathf.Max(0.1f, sprintDuration);
            lastSprintTime = Time.time;
            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
                IsSprinting = false;
            }
        }
        else if (Time.time - lastSprintTime > sprintRechargeDelay)
        {
            stamina = Mathf.Min(1f, stamina + dt / Mathf.Max(0.1f, sprintRecharge));
            if (exhausted && stamina >= sprintResumeThreshold) exhausted = false;
        }
    }

    // Los jugadores nunca pueden entrar en la red de una portería (zona definida por Goal)
    void KeepOutOfGoals()
    {
        if (Goal.All.Count == 0) return;

        Vector2 pos = rb.position;
        float radius = col.bounds.extents.x;
        bool moved = false;
        Vector2 normal = Vector2.zero;

        for (int i = 0; i < Goal.All.Count; i++)
        {
            Vector2 n;
            if (Goal.All[i].TryPushOut(ref pos, radius, out n))
            {
                moved = true;
                normal = n;
            }
        }

        if (!moved) return;

        rb.position = pos;

        // Anular la velocidad que empuja hacia dentro
        Vector2 v = rb.linearVelocity;
        float into = Vector2.Dot(v, normal);
        if (into < 0f) v -= normal * into;
        rb.linearVelocity = v;
    }

    // ---------- Bola ----------

    Vector2 HoldPosition()
    {
        Vector2 origin = transform.position;
        Vector2 desired = holdPoint != null ? (Vector2)holdPoint.position : origin + AimDir * CurrentHoldDistance;
        Vector2 delta = desired - origin;

        RaycastHit2D hit = Physics2D.CircleCast(origin, ball.col.bounds.extents.x, delta.normalized, delta.magnitude, wallLayer);
        return hit.collider != null ? hit.centroid - delta.normalized * 0.02f : desired;
    }

    // Recoger una bola SUELTA. Las bolas con dueño se roban con el stick, no por proximidad.
    void TryPickup()
    {
        if (Time.time < nextPickupTime) return;

        float radius = pickupRadius;
        Ball inst = Ball.Instance;
        if (inst != null && inst.IsPassFor(team)) radius *= receiverPickupBonus;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, radius, ballLayer);
        if (hit == null) return;

        Ball b = hit.GetComponent<Ball>();
        if (b == null || b.IsHeld) return;
        if (!CanControl(b)) return;

        TakeBall(b);
    }

    // Una bola demasiado rápida no se controla: pasa de largo (salvo un pase de un compañero)
    bool CanControl(Ball b)
    {
        float limit = b.IsPassFor(team) ? receiverMaxSpeed : autoPickupMaxSpeed;
        return b.Speed <= limit;
    }

    public void Shoot(Vector2 dir, float force)
    {
        if (!HasBall || !CanShoot) return;
        DropBall(dir.normalized * force + rb.linearVelocity * inheritVelocity, pickupCooldown, false);
        if (IsControlled) GameCamera.Shake(0.12f, 0.12f);   // pequeño golpe de cámara al tirar
    }

    // Tiro que sale EXACTAMENTE en 'dir': compensa la velocidad heredada del patinador
    public void ShootExact(Vector2 dir, float speed)
    {
        Vector2 desired = dir.normalized * speed;
        Vector2 cmd = desired - rb.linearVelocity * inheritVelocity;
        Shoot(cmd.normalized, cmd.magnitude);
    }

    float PassSpeedFor(float distance)
    {
        return Mathf.Clamp(distance * passSpeedPerMeter, passSpeedMin, passSpeedMax);
    }

    // ---------- Pases asistidos ----------
    // El pase no depende solo de la física: se elige un receptor (directo o rebotando en la banda)
    // y la bola corrige su rumbo hacia él durante el vuelo (ver Ball.SetGuide).

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return Vector2.Distance(p, a + ab * t);
    }

    // ¿Hay un rival sobre el segmento a-b? (el pase se interceptaría)
    bool LaneBlocked(Vector2 a, Vector2 b)
    {
        for (int i = 0; i < All.Count; i++)
        {
            Skater s = All[i];
            if (s.team == team) continue;
            if (DistToSegment(s.transform.position, a, b) < laneBlockRadius) return true;
        }
        return false;
    }

    // Pase directo: apunta a donde estará el compañero cuando llegue la bola
    PassPlan DirectPlan(Skater mate, Vector2 from)
    {
        Vector2 mp = mate.transform.position;
        float d0 = Vector2.Distance(from, mp);
        float flight = Mathf.Min(d0 / (PassSpeedFor(d0) * 0.7f), 0.6f);   // tiempo de vuelo aproximado
        Vector2 target = mp + mate.Velocity * flight;
        Vector2 dir = target - from;

        PassPlan plan;
        plan.mate = mate;
        plan.bank = false;
        plan.waypoint = target;
        plan.length = dir.magnitude;
        plan.firstDir = dir.sqrMagnitude > 0.01f ? dir.normalized : AimDir;
        return plan;
    }

    // Pase rebotando en una banda: se refleja al compañero en cada banda y se busca el punto de rebote
    bool TryBankPlan(Skater mate, Vector2 from, out PassPlan plan)
    {
        plan = default(PassPlan);

        Bounds b;
        if (!Rink.TryGetBounds(out b)) return false;

        Vector2 m = (Vector2)mate.transform.position + mate.Velocity * 0.3f;
        float minX = b.min.x + bankWallMargin, maxX = b.max.x - bankWallMargin;
        float minY = b.min.y + bankWallMargin, maxY = b.max.y - bankWallMargin;

        float bestLen = float.MaxValue;
        bool found = false;

        for (int w = 0; w < 4; w++)
        {
            bool vertical = w < 2;   // bandas izquierda/derecha (x constante)
            float wall = w == 0 ? minX : (w == 1 ? maxX : (w == 2 ? minY : maxY));

            Vector2 reflected;
            float t;
            if (vertical)
            {
                reflected = new Vector2(2f * wall - m.x, m.y);
                float den = reflected.x - from.x;
                if (Mathf.Abs(den) < 0.001f) continue;
                t = (wall - from.x) / den;
            }
            else
            {
                reflected = new Vector2(m.x, 2f * wall - m.y);
                float den = reflected.y - from.y;
                if (Mathf.Abs(den) < 0.001f) continue;
                t = (wall - from.y) / den;
            }

            if (t <= 0.05f || t >= 0.95f) continue;
            Vector2 hit = from + (reflected - from) * t;

            // El rebote debe caer en un tramo recto de la banda (lejos de las esquinas redondeadas)
            if (vertical)
            {
                if (hit.y < minY + bankCornerClearance || hit.y > maxY - bankCornerClearance) continue;
            }
            else
            {
                if (hit.x < minX + bankCornerClearance || hit.x > maxX - bankCornerClearance) continue;
            }

            float len = Vector2.Distance(from, hit) + Vector2.Distance(hit, m);
            if (len > bankMaxLength || len >= bestLen) continue;
            if (LaneBlocked(from, hit) || LaneBlocked(hit, m)) continue;

            bestLen = len;
            found = true;
            plan.mate = mate;
            plan.bank = true;
            plan.waypoint = hit;
            plan.length = len;
            plan.firstDir = (hit - from).normalized;
        }
        return found;
    }

    // Plan de pase a un compañero concreto (lo usa la IA): directo, o con rebote si el directo está bloqueado
    PassPlan PlanPass(Skater mate)
    {
        Vector2 from = ball.rb.position;
        PassPlan direct = DirectPlan(mate, from);
        if (!LaneBlocked(from, direct.waypoint)) return direct;

        PassPlan bank;
        if (TryBankPlan(mate, from, out bank)) return bank;
        return direct;
    }

    // Pase asistido para el humano: el más cercano a donde apuntas. Puede ser un compañero junto al cursor,
    // un compañero en la dirección del cursor, o uno al que se llega rebotando en la banda.
    // Sirve para ejecutar el pase y para mostrar la vista previa.
    public bool PlanSmartPass(Vector2 aimPoint, float cursorRadius, out PassPlan plan)
    {
        plan = default(PassPlan);
        if (!HasBall) return false;

        Vector2 from = ball.rb.position;
        Vector2 toAim = aimPoint - from;
        if (toAim.sqrMagnitude < 0.01f) toAim = AimDir;
        Vector2 aimDir = toAim.normalized;

        float bestAngle = passAssistAngle;
        bool found = false;

        for (int i = 0; i < All.Count; i++)
        {
            Skater mate = All[i];
            if (mate.team != team || mate == this) continue;

            // Directo
            PassPlan direct = DirectPlan(mate, from);
            float ang = Vector2.Angle(aimDir, direct.firstDir);
            if (Vector2.Distance(mate.transform.position, aimPoint) < cursorRadius) ang = 0f;   // cursor sobre el compañero
            if (ang < bestAngle) { bestAngle = ang; plan = direct; found = true; }

            // Rebotando en la banda (si el cursor apunta hacia ese rebote)
            PassPlan bank;
            if (TryBankPlan(mate, from, out bank))
            {
                float bAng = Vector2.Angle(aimDir, bank.firstDir) + 2f;   // ligera preferencia por el directo
                if (bAng < bestAngle) { bestAngle = bAng; plan = bank; found = true; }
            }
        }
        return found;
    }

    // Pase del humano: asistido si apunta a un compañero (directo o rebotado); si no, al punto indicado
    public void SmartPass(Vector2 aimPoint, float cursorRadius)
    {
        if (!HasBall || !CanPass) return;

        PassPlan plan;
        if (PlanSmartPass(aimPoint, cursorRadius, out plan)) ExecutePass(plan);
        else PassToPoint(aimPoint);
    }

    void ExecutePass(PassPlan plan)
    {
        float length = plan.length * (plan.bank ? 1.25f : 1f);   // el rebote le quita algo de velocidad
        Vector2 velocity = plan.firstDir * PassSpeedFor(length);

        DropBall(velocity, pickupCooldown, true, plan.mate, plan.bank ? (Vector2?)plan.waypoint : null);
        if (GameManager.Instance != null) GameManager.Instance.OnPass(this);
    }

    // Pase a un compañero (lo usa la IA): asistido, directo o con rebote
    public void PassTo(Skater mate)
    {
        if (!HasBall || !CanPass || mate == null) return;
        ExecutePass(PlanPass(mate));
    }

    // Pase al punto indicado (p. ej. el cursor) sin asistencia
    public void PassToPoint(Vector2 point)
    {
        if (!HasBall || !CanPass) return;

        Vector2 from = ball.rb.position;
        Vector2 dir = point - from;
        if (dir.sqrMagnitude < 0.01f) dir = AimDir;

        DropBall(dir.normalized * PassSpeedFor(dir.magnitude), pickupCooldown, true);
        if (GameManager.Instance != null) GameManager.Instance.OnPass(this);
    }

    // Te quitan la bola sin más (empujón); el robo con el stick usa GiveBallTo
    public void LoseBall(Vector2 pushDir)
    {
        if (!HasBall) return;
        DropBall(pushDir * 4f, 0.6f, false);
    }

    void DropBall(Vector2 velocity, float cooldown, bool isPass, Skater guideMate = null, Vector2? guideWaypoint = null)
    {
        Ball b = ball;
        ball = null;
        nextPickupTime = Time.time + cooldown;
        b.Release(velocity);
        if (isPass) b.MarkPass(team, passWindow);
        if (guideMate != null) b.SetGuide(guideMate, guideWaypoint);
        StartCoroutine(ReenableCollision(b, cooldown));
    }

    IEnumerator ReenableCollision(Ball b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics2D.IgnoreCollision(col, b.col, false);
    }

    // ---------- Stick: robar o interceptar ----------

    // Golpe de stick hacia AimDir. Es una apuesta: si no consigue la bola, el atacante queda descolocado.
    public StickResult Stick()
    {
        if (HasBall || !CanMove || IsStaggered) return StickResult.None;
        if (Time.time < nextSwingTime) return StickResult.None;
        if (useStealCooldown && Time.time < nextStealTime) return StickResult.Cooldown;

        nextSwingTime = Time.time + minSwingInterval;
        if (useStealCooldown) nextStealTime = Time.time + stealCooldown;

        Vector2 pos = transform.position;
        Feedback.Swing(pos, AimDir, stickReach, stickArc);

        Ball b = Ball.Instance;
        if (b == null) return Miss(StickResult.Whiff, missRecovery);

        Vector2 to = b.rb.position - pos;
        if (to.magnitude > stickReach || Vector2.Angle(AimDir, to) > stickArc * 0.5f)
            return Miss(StickResult.Whiff, missRecovery);   // golpe al aire: queda descolocado

        // Bola de un rival: intento de robo
        if (b.IsHeld)
        {
            if (b.Holder.team == team) return StickResult.Whiff;
            return b.Holder.ResolveSteal(this);
        }

        // Bola suelta
        if (Time.time < nextPickupTime) return Miss(StickResult.Whiff, missRecovery * 0.5f);

        if (b.Speed > stickCatchMaxSpeed)
        {
            Feedback.Popup(b.rb.position, "¡MUY RÁPIDA!", colInfo);
            return Miss(StickResult.TooFast, missRecovery * 0.5f);
        }

        // Los pases rápidos no se controlan siempre: a veces solo se toca y se desvía
        if (b.Speed > autoPickupMaxSpeed)
        {
            float chance = interceptSuccessChance * Mathf.Lerp(0.7f, 1.3f, stealRating);
            if (Random.value > chance)
            {
                b.rb.linearVelocity = Rotate(b.rb.linearVelocity, Random.Range(-25f, 25f)) * 0.75f;
                b.Touch(team);
                Feedback.Popup(b.rb.position, "¡TOCADA!", colInfo);
                return Miss(StickResult.Failed, missRecovery * 0.5f);
            }
        }

        Team other = team == Team.Home ? Team.Away : Team.Home;
        bool wasEnemyPass = b.IsPassFor(other);

        TakeBall(b);
        Feedback.Popup(pos, wasEnemyPass ? "¡INTERCEPTADA!" : "¡RECOGIDA!", colGood);
        return StickResult.Intercepted;
    }

    StickResult Miss(StickResult result, float recovery)
    {
        Stagger(recovery);
        return result;
    }

    // Lo expuesta que está la bola hacia una posición: 1 = la bola está entre el portador y esa posición,
    // -1 = el portador la tapa con el cuerpo. Lo usan la IA y la defensa asistida para golpear con sentido.
    public float BallExposureTo(Vector2 attackerPos)
    {
        if (ball == null) return -1f;
        Vector2 pos = transform.position;
        Vector2 ballDir = (ball.rb.position - pos).normalized;
        Vector2 toAttacker = (attackerPos - pos).normalized;
        return Vector2.Dot(ballDir, toAttacker);
    }

    // Probabilidad de robo según el ÁNGULO: entrar por el lado de la bola es casi seguro,
    // por el lateral es una apuesta. Deja de ser una tirada fija y pasa a ser una habilidad.
    float StealChanceAgainst(Skater carrier, float side)
    {
        float t = Mathf.InverseLerp(shieldDot, 0.85f, side);
        float chance = Mathf.Lerp(stealBaseChance, stealMaxChance, t);

        if (carrier.IsSprinting) chance += sprintStealBonus;

        chance *= Mathf.Lerp(0.8f, 1.2f, stealRating) * Mathf.Lerp(1.2f, 0.8f, carrier.ballControl);
        return Mathf.Clamp(chance, 0.05f, 0.9f);
    }

    // Lo llama el atacante sobre el que lleva la bola. Cada fallo deja descolocado al atacante.
    StickResult ResolveSteal(Skater thief)
    {
        Vector2 pos = transform.position;

        // 1) En pleno regate: no se puede robar
        if (IsDribbling)
        {
            thief.Stagger(thief.missRecovery);
            Feedback.Popup(pos, "¡REGATE!", colWarn);
            return StickResult.Dribbled;
        }

        // 2) Acaba de recibir la bola
        if (Time.time < graceUntil)
        {
            thief.Stagger(thief.missRecovery);
            Feedback.Popup(pos, "¡AGUANTA!", colInfo);
            return StickResult.Grace;
        }

        // 3) Protegida con el cuerpo: el atacante queda del lado contrario a la bola
        Vector2 ballDir = (ball.rb.position - pos).normalized;
        Vector2 toThief = ((Vector2)thief.transform.position - pos).normalized;
        float side = Vector2.Dot(ballDir, toThief);
        if (side < shieldDot)
        {
            thief.Stagger(thief.missRecovery);
            Feedback.Popup(pos, "¡PROTEGIDA!", colInfo);
            return StickResult.Shielded;
        }

        // 4) Tirada según el ángulo de entrada
        float chance = thief.StealChanceAgainst(this, side);
        bool success = Random.value < chance;

        if (thief.logSteals)
            Debug.Log($"Robo {thief.name} -> {name}: probabilidad {chance:P0} -> {(success ? "ROBA" : "falla")}");

        if (!success)
        {
            thief.Stagger(thief.missRecovery * (side > 0.5f ? 0.5f : 1f));   // un buen intento fallido castiga menos
            Feedback.Popup(pos, "¡AGUANTÓ!", colInfo);
            return StickResult.Failed;
        }

        GiveBallTo(thief);
        return StickResult.Stolen;
    }

    void GiveBallTo(Skater thief)
    {
        Ball b = ball;
        ball = null;
        nextPickupTime = Time.time + 0.6f;
        Physics2D.IgnoreCollision(col, b.col, false);
        b.Release(Vector2.zero);

        Stagger(0.5f);                       // el que pierde la bola queda descolocado un momento
        thief.TakeBall(b);                   // el ladrón se la queda directamente

        Feedback.Popup(thief.transform.position, "¡ROBO!", colGood);
        Feedback.Popup(transform.position, "¡PERDIDA!", colBad);
    }

    public void Stagger(float duration)
    {
        staggerUntil = Mathf.Max(staggerUntil, Time.time + duration);
    }

    // ---------- Regate (juke) ----------

    // Impulso rápido con la bola. Durante el regate no te pueden robar y los rivales cercanos
    // quedan descolocados. Sin dirección (Vector2.zero) corta hacia el lado libre del rival más cercano.
    public bool Juke(Vector2 dir)
    {
        if (!CanJuke) return false;

        if (dir.sqrMagnitude < 0.01f) dir = AutoJukeDir();
        jukeDir = dir.normalized;
        jukeUntil = Time.time + jukeDuration;
        nextJukeTime = Time.time + jukeCooldown;
        stamina = Mathf.Max(0f, stamina - jukeStaminaCost);
        lastSprintTime = Time.time;

        int beaten = 0;
        for (int i = 0; i < All.Count; i++)
        {
            Skater s = All[i];
            if (s.team == team) continue;
            if (Vector2.Distance(s.transform.position, transform.position) > jukeBeatRange) continue;
            s.Stagger(jukeBeatTime);
            beaten++;
        }

        if (beaten > 0) Feedback.Popup(transform.position, "¡REGATE!", colWarn);
        return true;
    }

    Vector2 AutoJukeDir()
    {
        Skater near = null;
        float best = float.MaxValue;
        for (int i = 0; i < All.Count; i++)
        {
            Skater s = All[i];
            if (s.team == team) continue;
            float d = Vector2.Distance(s.transform.position, transform.position);
            if (d < best) { best = d; near = s; }
        }

        Vector2 fwd = rb.linearVelocity.sqrMagnitude > 1f ? rb.linearVelocity.normalized : AimDir;
        if (near == null) return fwd;

        Vector2 away = ((Vector2)transform.position - (Vector2)near.transform.position).normalized;
        Vector2 perp = new Vector2(-away.y, away.x);
        if (Vector2.Dot(perp, fwd) < 0f) perp = -perp;
        return (perp + fwd * 0.5f).normalized;   // corte lateral con algo de avance
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ---------- Reinicios ----------

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
        SprintInput = false;

        stamina = 1f;
        exhausted = false;
        jukeUntil = 0f;
        staggerUntil = 0f;
        graceUntil = 0f;
        nextStealTime = 0f;
        nextJukeTime = 0f;
    }

    public void MirrorAnchor()
    {
        Anchor = new Vector2(-Anchor.x, Anchor.y);
    }

    public void TakeBall(Ball b)
    {
        ball = b;
        ball.Grab(this);
        Physics2D.IgnoreCollision(col, ball.col, true); // no choca con quien la lleva
        graceUntil = Time.time + possessionGrace;
    }

    public void TeleportTo(Vector2 pos)
    {
        transform.position = pos;
        rb.position = pos;
        rb.linearVelocity = Vector2.zero;
        MoveInput = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stickReach);
    }

    bool CanMove => GameManager.Instance == null || GameManager.Instance.CanMove(this);
    bool CanShoot => GameManager.Instance == null || GameManager.Instance.CanShoot;
    bool CanPass => GameManager.Instance == null || GameManager.Instance.CanPass;
}