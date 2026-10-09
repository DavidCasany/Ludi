using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StickResult { None, Cooldown, Whiff, Stolen, Intercepted, TooFast, Shielded, Dribbled, Grace, Failed }

// El "cuerpo" del jugador. No lee input: otro script (TeamManager para el humano,
// SkaterAI para la IA) le dice hacia dónde moverse, a dónde apuntar y qué acciones hacer.
//
// REGLAS DE LA BOLA
//  - Una bola SUELTA solo se queda automáticamente si va despacio (autoPickupMaxSpeed).
//    Los pases de un compañero se controlan hasta receiverMaxSpeed. Los rivales no pueden
//    quedarse un pase rápido por el simple hecho de estar cerca.
//  - La bola de un rival NO se roba por proximidad: se roba con el STICK (Stick()).
//    Un defensa puede interceptar con el stick una bola suelta rápida (hasta stickCatchMaxSpeed).
//  - Te la pueden robar si te atacan por el lado de la bola. No pueden si la proteges con el cuerpo,
//    si estás en pleno regate o si acabas de recibirla (possessionGrace).
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

    [Header("Control de la bola (recogida)")]
    public float autoPickupMaxSpeed = 7f;   // velocidad máxima para quedarse una bola suelta por proximidad
    public float receiverMaxSpeed = 24f;    // ... si es un pase de un compañero
    public float receiverPickupBonus = 1.3f;// el receptor de un pase tiene más radio de recogida

    [Header("Stick: robo e intercepción")]
    public float stickReach = 1.4f;                 // alcance del stick desde el centro del jugador
    [Range(20f, 180f)] public float stickArc = 110f;// ángulo (total) del arco frontal del stick
    public float stickCatchMaxSpeed = 18f;          // velocidad máxima de una bola suelta que se puede interceptar
    public float minSwingInterval = 0.2f;           // mínimo entre golpes (aunque no haya cooldown)
    public bool useStealCooldown = true;            // ¿hay cooldown entre intentos de robo/intercepción?
    public float stealCooldown = 0.8f;              // duración del cooldown (si useStealCooldown está activo)
    [Range(0f, 1f)] public float stealSuccessChance = 1f; // probabilidad de robo si el golpe es válido
    public float possessionGrace = 0.6f;            // tras recibir la bola no te la pueden robar
    [Range(-1f, 1f)] public float shieldDot = -0.2f;// la bola está protegida si el atacante queda por debajo de este valor

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

    public bool IsSprinting { get; private set; }
    public bool IsExhausted => exhausted;
    public float Stamina01 => stamina;
    public bool IsDribbling => Time.time < jukeUntil;
    public bool IsStaggered => Time.time < staggerUntil;
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
            rb.linearVelocity = jukeDir * jukeSpeed;
            IsSprinting = false;
            return;
        }

        Vector2 input = Vector2.ClampMagnitude(MoveInput, 1f);
        bool moving = input.sqrMagnitude > 0.01f;

        UpdateSprint(moving, dt);

        float sprintMul = IsSprinting ? sprintMultiplier : 1f;
        float slowMul = IsStaggered ? staggerSlow : 1f;

        Vector2 target = input * maxSpeed * sprintMul * slowMul;
        float rate = moving ? acceleration * sprintMul : deceleration;
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
    }

    float PassSpeedFor(float distance)
    {
        return Mathf.Clamp(distance * passSpeedPerMeter, passSpeedMin, passSpeedMax);
    }

    // Pase a un compañero: rápido (≈ 0.5 s de vuelo) y anticipando su movimiento
    public void PassTo(Skater mate)
    {
        if (!HasBall || !CanPass || mate == null) return;

        Vector2 from = ball.rb.position;
        Vector2 mp = mate.transform.position;
        float d0 = Vector2.Distance(from, mp);

        float flight = Mathf.Min(d0 / (PassSpeedFor(d0) * 0.7f), 0.6f);   // tiempo de vuelo aproximado
        Vector2 target = mp + mate.Velocity * flight;

        Vector2 dir = target - from;
        if (dir.sqrMagnitude < 0.01f) dir = AimDir;

        DropBall(dir.normalized * PassSpeedFor(dir.magnitude), pickupCooldown, true);
        if (GameManager.Instance != null) GameManager.Instance.OnPass(this);
    }

    // Pase al punto indicado (p. ej. el cursor)
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

    void DropBall(Vector2 velocity, float cooldown, bool isPass)
    {
        Ball b = ball;
        ball = null;
        nextPickupTime = Time.time + cooldown;
        b.Release(velocity);
        if (isPass) b.MarkPass(team, passWindow);
        StartCoroutine(ReenableCollision(b, cooldown));
    }

    IEnumerator ReenableCollision(Ball b, float delay)
    {
        yield return new WaitForSeconds(delay);
        Physics2D.IgnoreCollision(col, b.col, false);
    }

    // ---------- Stick: robar o interceptar ----------

    // Golpe de stick hacia AimDir. Roba la bola de un rival si está en alcance y arco,
    // o intercepta una bola suelta rápida. Devuelve qué ha pasado.
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
        if (b == null) return StickResult.Whiff;

        Vector2 to = b.rb.position - pos;
        if (to.magnitude > stickReach || Vector2.Angle(AimDir, to) > stickArc * 0.5f)
            return StickResult.Whiff;

        // Bola de un rival: intento de robo
        if (b.IsHeld)
        {
            if (b.Holder.team == team) return StickResult.Whiff;
            return b.Holder.ResolveSteal(this);
        }

        // Bola suelta: intercepción
        if (Time.time < nextPickupTime) return StickResult.Whiff;

        if (b.Speed > stickCatchMaxSpeed)
        {
            Feedback.Popup(b.rb.position, "¡MUY RÁPIDA!", colInfo);
            return StickResult.TooFast;
        }

        Team other = team == Team.Home ? Team.Away : Team.Home;
        bool wasEnemyPass = b.IsPassFor(other);

        TakeBall(b);
        Feedback.Popup(pos, wasEnemyPass ? "¡INTERCEPTADA!" : "¡RECOGIDA!", colGood);
        return StickResult.Intercepted;
    }

    // Lo llama el atacante sobre el que lleva la bola. Reglas claras y siempre con motivo visible.
    StickResult ResolveSteal(Skater thief)
    {
        Vector2 pos = transform.position;
        Vector2 popupPos = pos;

        // 1) En pleno regate: no se puede robar
        if (IsDribbling)
        {
            Feedback.Popup(popupPos, "¡REGATE!", colWarn);
            return StickResult.Dribbled;
        }

        // 2) Acaba de recibir la bola
        if (Time.time < graceUntil)
        {
            Feedback.Popup(popupPos, "¡AGUANTA!", colInfo);
            return StickResult.Grace;
        }

        // 3) Protegida con el cuerpo: el atacante queda del lado contrario a la bola
        Vector2 ballDir = (ball.rb.position - pos).normalized;
        Vector2 toThief = ((Vector2)thief.transform.position - pos).normalized;
        if (Vector2.Dot(ballDir, toThief) < shieldDot)
        {
            Feedback.Popup(popupPos, "¡PROTEGIDA!", colInfo);
            return StickResult.Shielded;
        }

        // 4) Probabilidad opcional (1 = siempre que el golpe sea válido)
        if (Random.value > stealSuccessChance)
        {
            Feedback.Popup(popupPos, "¡FALLO!", colInfo);
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