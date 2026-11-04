using UnityEngine;

// Part VISUAL del porter (la física i les parades són a Goalkeeper).
//  - El COS (Visual) mira la bola, però sempre cap a la pista: mai d'esquena a la pista.
//  - L'ESTIC (StickPivot) segueix la bola, limitat a un arc davant del cos.
//  - POSTURA DE PARADA: quan Goalkeeper avisa (Saved), durant uns instants obre les guardes en
//    "papallona", estira el guant gran cap al costat de la bola i fa un petit cop d'escala.
// Els colors (samarreta i guants, i el flaix groc de les parades) els posa Goalkeeper amb els seus kitRenderers.
//
// JERARQUIA (la crea Hockey > Muntar capes del porter)
//  GoalKeeper (Rigidbody2D Kinematic, BoxCollider2D, Goalkeeper, GoalkeeperVisual)
//   └ Visual (SortingGroup)                   <- body
//      ├ Shadow, Base (guardes), Jersey, Head
//      ├ BigGlove   (guant gran, mà lliure)   <- bigGlove
//      └ StickPivot                            <- stickPivot
//         ├ Stick, Gloves (manyopla)
//         └ BladeTip                           <- bladeTip
public class GoalkeeperVisual : MonoBehaviour
{
    [SerializeField] Transform body;
    [SerializeField] Transform stickPivot;
    [SerializeField] Transform bladeTip;

    [SerializeField] float bodyTurnSpeed = 360f;
    [Tooltip("Com de lluny del davant (cap a la pista) pot girar el cos, en graus")]
    [SerializeField, Range(0f, 180f)] float maxBodyAngle = 75f;
    [SerializeField] float stickTurnSpeed = 720f;
    [SerializeField, Range(0f, 180f)] float maxStickAngle = 100f;

    [Header("Postura de parada")]
    [SerializeField] SpriteRenderer shadowRenderer;
    [SerializeField] SpriteRenderer baseRenderer;
    [SerializeField] SpriteRenderer bigGlove;
    [SerializeField] Sprite shadowIdle, shadowSave;
    [SerializeField] Sprite baseIdle, baseSave;
    [SerializeField] Sprite gloveIdle, gloveSave;
    [Tooltip("Quant de temps es queda en postura de parada (s, temps real: funciona encara que hi hagi bullet time)")]
    [SerializeField] float savePoseTime = 0.45f;
    [Tooltip("Cop d'escala del cos en començar la parada")]
    [SerializeField] float savePunch = 0.12f;

    Goalkeeper keeper;
    float bladeLocalAngle;
    float stickAngle;
    float saveStart = -10f;
    int saveSide = 1;   // +1 = la bola és a la dreta del cos (costat del guant), -1 = a l'esquerra
    bool posed;

    void Awake()
    {
        keeper = GetComponent<Goalkeeper>();

        if (bladeTip != null && stickPivot != null)
        {
            Vector2 local = stickPivot.InverseTransformPoint(bladeTip.position);
            bladeLocalAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }
        stickAngle = Outward();
        if (body != null) body.rotation = Quaternion.Euler(0f, 0f, Outward() - 90f);

        // Si no s'han assignat els sprites "quiets", agafa els que ja porten els renderers
        if (shadowRenderer != null && shadowIdle == null) shadowIdle = shadowRenderer.sprite;
        if (baseRenderer != null && baseIdle == null) baseIdle = baseRenderer.sprite;
        if (bigGlove != null && gloveIdle == null) gloveIdle = bigGlove.sprite;
        SetPose(false);
    }

    void OnEnable() { if (keeper != null) keeper.Saved += OnSaved; }
    void OnDisable() { if (keeper != null) keeper.Saved -= OnSaved; }

    void OnSaved()
    {
        saveStart = Time.unscaledTime;

        // A quin costat del cos és la bola? (x local del cos: el sprite mira cap a +Y)
        Ball ball = Ball.Instance;
        if (ball != null && body != null)
        {
            float localX = body.InverseTransformPoint(ball.rb.position).x;
            saveSide = localX >= 0f ? 1 : -1;
        }
    }

    // Cap a on és la pista: la porteria és a un costat (x) i el centre de la pista a x = 0
    float Outward() => transform.position.x > 0f ? 180f : 0f;

    void LateUpdate()
    {
        Ball ball = Ball.Instance;
        Vector2 pos = transform.position;
        float outward = Outward();
        float toBall = ball != null ? Angle(ball.rb.position - pos) : outward;

        // Cos: mira la bola sense girar-se d'esquena a la pista
        float bodyTarget = outward + Mathf.Clamp(Mathf.DeltaAngle(outward, toBall), -maxBodyAngle, maxBodyAngle);
        float bodyForward = outward;
        if (body != null)
        {
            float z = Mathf.MoveTowardsAngle(body.eulerAngles.z, bodyTarget - 90f, bodyTurnSpeed * Time.deltaTime);
            body.rotation = Quaternion.Euler(0f, 0f, z);   // el sprite mira cap amunt (+Y)
            bodyForward = z + 90f;
        }

        UpdateSavePose();

        // Estic: la pala apunta a la bola, dins d'un arc davant del cos
        if (stickPivot == null) return;
        float target = bodyForward + Mathf.Clamp(Mathf.DeltaAngle(bodyForward, toBall), -maxStickAngle, maxStickAngle);
        stickAngle = Mathf.MoveTowardsAngle(stickAngle, target, stickTurnSpeed * Time.deltaTime);
        stickPivot.rotation = Quaternion.Euler(0f, 0f, stickAngle - bladeLocalAngle);
    }

    void UpdateSavePose()
    {
        float t = (Time.unscaledTime - saveStart) / Mathf.Max(0.01f, savePoseTime);
        bool saving = t >= 0f && t < 1f;
        if (saving != posed) SetPose(saving);

        if (body != null)
        {
            // Cop d'escala: puja ràpid i torna suau
            float punch = saving ? Mathf.Sin(Mathf.Clamp01(t * 3f) * Mathf.PI) * savePunch : 0f;
            body.localScale = Vector3.one * (1f + punch);
        }

        // El guant gran va cap al costat de la bola (mirall en X si la bola és a l'esquerra)
        if (bigGlove != null)
        {
            Vector3 s = bigGlove.transform.localScale;
            s.x = saving ? saveSide : 1f;
            bigGlove.transform.localScale = s;
        }
    }

    void SetPose(bool saving)
    {
        posed = saving;
        if (shadowRenderer != null && shadowSave != null) shadowRenderer.sprite = saving ? shadowSave : shadowIdle;
        if (baseRenderer != null && baseSave != null) baseRenderer.sprite = saving ? baseSave : baseIdle;
        if (bigGlove != null && gloveSave != null) bigGlove.sprite = saving ? gloveSave : gloveIdle;
    }

    static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
}
