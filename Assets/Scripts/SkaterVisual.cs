using UnityEngine;

// Part VISUAL del jugador. No toca la física: només gira capes i les tenyeix.
//
//  - El COS (Visual) mira cap a on va el jugador (o cap a on apunta si porta la bola).
//  - L'ESTIC (StickPivot) segueix la bola tot el partit, limitat a un arc davant del cos.
//    Si la bola és a prop, l'estic "s'estira o s'arronsa" perquè la pala quedi just a la bola.
//  - Fa una escombrada quan el jugador intenta robar (Skater.Swung) i un cop sec en xutar (Skater.Shot).
//  - SetKit() pinta la samarreta, el patró (franges, banda...) i els guants amb els colors de l'equip.
//
// JERARQUIA ESPERADA
//  Skater (Rigidbody2D, Collider2D, Skater, SkaterAI, SkaterVisual)
//   └ Visual            (SortingGroup)           <- body
//      ├ Shadow          order -1
//      ├ Base            order 0   (cames i patins, sense tenyir)
//      ├ Jersey          order 1   (samarreta en grisos)          <- primaryRenderers
//      ├ Pattern         order 2   (patró de l'equip, en grisos)  <- patternRenderer
//      ├ Head            order 4   (cap, sense tenyir)
//      └ StickPivot                                <- stickPivot (al centre del cos)
//         ├ Stick        order 3
//         ├ Gloves       order 5   (guants en grisos)             <- secondaryRenderers
//         └ BladeTip     (Empty a la pala, on toca la bola)       <- bladeTip
[RequireComponent(typeof(Skater))]
public class SkaterVisual : MonoBehaviour
{
    [Header("Capes")]
    [SerializeField] Transform body;
    [SerializeField] Transform stickPivot;
    [SerializeField] Transform bladeTip;
    [Tooltip("Es tenyeixen amb el color principal (samarreta)")]
    [SerializeField] SpriteRenderer[] primaryRenderers;
    [Tooltip("Es tenyeixen amb el color secundari (guants, ribets, número)")]
    [SerializeField] SpriteRenderer[] secondaryRenderers;
    [Tooltip("Capa del patró: rep el sprite i el color del TeamData")]
    [SerializeField] SpriteRenderer patternRenderer;

    [Header("Cos")]
    [SerializeField] float bodyTurnSpeed = 540f;      // graus/s
    [SerializeField] float faceVelocityMin = 1.2f;    // per sota d'aquesta velocitat mira la bola
    [SerializeField, Range(0f, 15f)] float leanAngle = 6f;   // inclinació en girar (dona sensació de patinar)

    [Header("Estic")]
    [SerializeField] float stickTurnSpeed = 720f;
    [SerializeField, Range(0f, 180f)] float maxStickAngle = 110f;   // respecte al davant del cos
    [SerializeField] float maxPull = 0.35f;           // quant pot arronsar l'estic cap al cos
    [SerializeField] float pullSpeed = 5f;

    [Header("Animacions de cop")]
    [SerializeField] float swingArc = 100f;
    [SerializeField] float swingTime = 0.16f;
    [SerializeField] float shotFlick = 40f;
    [SerializeField] float shotTime = 0.18f;

    Skater skater;
    float bladeLocalAngle;   // angle de la pala dins del sprite (sense girar)
    float bladeLength;       // distància pivot -> pala (en unitats de món)
    float stickAngle;        // angle actual de la pala (món)
    float pull;
    float swingUntil = -1f, shotUntil = -1f;
    float lastBodyAngle;

    void Awake()
    {
        skater = GetComponent<Skater>();

        if (bladeTip != null && stickPivot != null)
        {
            Vector2 local = stickPivot.InverseTransformPoint(bladeTip.position);
            bladeLocalAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            bladeLength = Vector2.Distance(stickPivot.position, bladeTip.position);
        }

        lastBodyAngle = body != null ? body.eulerAngles.z : 0f;
        stickAngle = lastBodyAngle + 90f;
    }

    void OnEnable()
    {
        skater.Swung += OnSwing;
        skater.Shot += OnShot;
    }

    void OnDisable()
    {
        skater.Swung -= OnSwing;
        skater.Shot -= OnShot;
    }

    void OnSwing() { swingUntil = Time.time + swingTime; }
    void OnShot() { shotUntil = Time.time + shotTime; }

    // ---------- Equipació ----------

    public void SetKit(Color primary, Color secondary) => SetKit(primary, secondary, null, secondary);

    // shirt = samarreta, trim = guants i ribets, pattern = patró (null = llisa), patternColor = color del patró
    public void SetKit(Color shirt, Color trim, Sprite pattern, Color patternColor)
    {
        foreach (SpriteRenderer sr in primaryRenderers) if (sr != null) sr.color = shirt;
        foreach (SpriteRenderer sr in secondaryRenderers) if (sr != null) sr.color = trim;

        if (patternRenderer != null)
        {
            patternRenderer.sprite = pattern;
            patternRenderer.color = patternColor;
            patternRenderer.enabled = pattern != null;
        }
    }

    // ---------- Animació ----------

    // LateUpdate: la física i la IA ja han decidit on és tot aquest frame
    void LateUpdate()
    {
        Ball ball = Ball.Instance;
        Vector2 pos = transform.position;
        Vector2 toBall = ball != null ? ball.rb.position - pos : skater.AimDir;

        UpdateBody(toBall);
        if (stickPivot != null) UpdateStick(pos, toBall);
    }

    void UpdateBody(Vector2 toBall)
    {
        if (body == null) return;

        Vector2 dir;
        if (skater.HasBall) dir = skater.AimDir;                                   // porta la bola: mira on apunta
        else if (skater.Velocity.magnitude > faceVelocityMin) dir = skater.Velocity; // patinant: mira on va
        else dir = toBall;                                                          // quiet: mira la bola

        float target = Angle(dir) - 90f;   // el sprite mira cap amunt (+Y)
        float z = Mathf.MoveTowardsAngle(body.eulerAngles.z, target, bodyTurnSpeed * Time.deltaTime);

        // Inclinació en girar: una mica d'escala lateral segons la velocitat de gir
        float turnRate = Mathf.DeltaAngle(lastBodyAngle, z) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastBodyAngle = z;
        float lean = Mathf.Clamp(turnRate / bodyTurnSpeed, -1f, 1f) * leanAngle;

        body.rotation = Quaternion.Euler(0f, 0f, z + lean * 0.3f);
        float squash = 1f - Mathf.Abs(lean) * 0.004f;
        body.localScale = new Vector3(squash, 1f, 1f);
    }

    void UpdateStick(Vector2 pos, Vector2 toBall)
    {
        float bodyForward = body != null ? body.eulerAngles.z + 90f : Angle(skater.AimDir);

        // 1) La pala apunta a la bola, però mai per darrere de l'esquena
        float toBallAngle = Angle(toBall);
        float delta = Mathf.Clamp(Mathf.DeltaAngle(bodyForward, toBallAngle), -maxStickAngle, maxStickAngle);
        float target = bodyForward + delta;

        float speed = skater.HasBall ? stickTurnSpeed * 2f : stickTurnSpeed;   // amb bola, enganxada
        stickAngle = Mathf.MoveTowardsAngle(stickAngle, target, speed * Time.deltaTime);

        // 2) Cops: escombrada (robar) i seguiment del xut
        float offset = 0f;
        if (Time.time < swingUntil)
        {
            float t = 1f - (swingUntil - Time.time) / swingTime;
            offset = Mathf.Lerp(swingArc * 0.5f, -swingArc * 0.5f, EaseOut(t));
        }
        else if (Time.time < shotUntil)
        {
            float t = 1f - (shotUntil - Time.time) / shotTime;
            offset = -Mathf.Sin(t * Mathf.PI) * shotFlick;
        }

        float blade = stickAngle + offset;
        stickPivot.rotation = Quaternion.Euler(0f, 0f, blade - bladeLocalAngle);

        // 3) Si la bola és a prop, l'estic s'arronsa perquè la pala hi quedi a sobre
        float dist = toBall.magnitude;
        float wantPull = 0f;
        if (skater.HasBall || dist < bladeLength + 0.5f)
            wantPull = Mathf.Clamp(bladeLength - dist, 0f, maxPull);
        pull = Mathf.MoveTowards(pull, wantPull, pullSpeed * Time.deltaTime);

        Vector2 bladeDir = new Vector2(Mathf.Cos(blade * Mathf.Deg2Rad), Mathf.Sin(blade * Mathf.Deg2Rad));
        stickPivot.position = pos - bladeDir * pull;
    }

    static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
