using UnityEngine;
using UnityEngine.UI;

// Anima la imatge del 1 VS 1 com les pantalles VS dels jocs de lluita:
//  - Cada pocs segons cau un "tret" de llamp: 2 o 3 flaixos ràpids i irregulars (com un llamp de veritat),
//    el llamp tremola, el VS fa un cop de mida i tota la imatge es sacseja una mica.
//  - Entre trets, el llamp brilla suau i el VS "respira".
// Va amb dues capes extretes de la mateixa imatge: la llum del llamp (blanca, es tenyeix) i les lletres VS.
// No canvia la imatge de base: les capes van a sobre, exactament alineades.
public class VsBannerFX : MenuPreviewFX
{
    [Header("Capes")]
    [SerializeField] Image boltGlow;
    [SerializeField] RectTransform letters;
    [Tooltip("Centre de les lletres VS a la imatge (0..1)")]
    [SerializeField] Vector2 lettersCenter = new Vector2(0.507f, 0.509f);

    [Header("Llamp")]
    [SerializeField] Color glowColor = new Color(1f, 0.97f, 0.82f, 1f);
    [SerializeField, Range(0f, 1f)] float idleGlow = 0.12f;
    [SerializeField, Range(0f, 1f)] float flashGlow = 0.85f;
    [SerializeField] Vector2 strikeInterval = new Vector2(1.4f, 3f);   // segons entre trets (mín, màx)
    [SerializeField] float flashDecay = 0.09f;                         // com de ràpid s'apaga cada flaix
    [SerializeField] float boltJitter = 5f;                            // tremolor del llamp (px)

    [Header("VS i sacsejada")]
    [SerializeField] float punchScale = 1.14f;
    [SerializeField] float punchTime = 0.3f;
    [SerializeField, Range(0f, 0.05f)] float breathe = 0.018f;
    [SerializeField] float shakeAmount = 6f;                           // px
    [SerializeField] float shakeTime = 0.22f;

    RectTransform shakeTarget;      // el Preview sencer (imatge de base + efectes)
    Vector2 shakeBase;
    bool shaking;

    float nextStrike;
    readonly float[] flashes = new float[3];   // moments dels flaixos del tret actual
    int flashCount;
    float strikeTime = -10f;
    bool wasVisible;

    protected override void Awake()
    {
        base.Awake();
        shakeTarget = transform.parent as RectTransform;
    }

    void OnDisable() => StopShake();

    protected override void LateUpdate()
    {
        base.LateUpdate();
        float now = Time.unscaledTime;

        if (!IsVisible)
        {
            wasVisible = false;
            StopShake();
            return;
        }

        // En aparèixer la imatge: un tret de seguida, perquè es vegi viva des del primer moment
        if (!wasVisible) { wasVisible = true; Strike(now + 0.12f); }
        if (now >= nextStrike) Strike(now);

        // ---------- Llum del llamp ----------
        float flash = 0f;
        for (int i = 0; i < flashCount; i++)
        {
            float dt = now - flashes[i];
            if (dt >= 0f) flash = Mathf.Max(flash, Mathf.Exp(-dt / flashDecay));
        }
        float idle = idleGlow * (0.75f + 0.25f * Mathf.Sin(now * 5.3f) * Mathf.Sin(now * 2.1f));
        if (boltGlow != null)
        {
            Color c = glowColor;
            c.a = Mathf.Clamp01(idle + flash * flashGlow);
            boltGlow.color = c;
            boltGlow.rectTransform.anchoredPosition = flash > 0.2f ? Random.insideUnitCircle * boltJitter * flash : Vector2.zero;
        }

        // ---------- Lletres VS: cop de mida al tret + respiració ----------
        if (letters != null)
        {
            float t = (now - strikeTime) / punchTime;
            // molla amortida: salta a punchScale i torna rebotant (per sota d'1 no baixa: Max(1, s))
            float punch = t >= 0f && t < 1f ? (punchScale - 1f) * Mathf.Exp(-5f * t) * Mathf.Cos(t * 10f) : 0f;
            float s = 1f + punch + breathe * Mathf.Sin(now * 2.4f);
            ScaleAround(letters, Mathf.Max(1f, s), ImagePoint(lettersCenter));
        }

        // ---------- Sacsejada ----------
        float st = (now - strikeTime) / shakeTime;
        if (shakeTarget != null && st >= 0f && st < 1f)
        {
            if (!shaking) { shakeBase = shakeTarget.anchoredPosition; shaking = true; }
            shakeTarget.anchoredPosition = shakeBase + Random.insideUnitCircle * shakeAmount * (1f - st);
        }
        else StopShake();
    }

    // Programa un tret: 2 o 3 flaixos separats de manera irregular
    void Strike(float at)
    {
        strikeTime = at;
        flashCount = Random.value < 0.6f ? 2 : 3;
        float t = at;
        for (int i = 0; i < flashCount; i++)
        {
            flashes[i] = t;
            t += Random.Range(0.06f, 0.14f);
        }
        nextStrike = at + Random.Range(strikeInterval.x, strikeInterval.y);
    }

    void StopShake()
    {
        if (!shaking) return;
        shaking = false;
        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeBase;
    }

    // Escala un RectTransform estirat (anchors 0..1) al voltant d'un punt del pare, sense tocar el pivot
    void ScaleAround(RectTransform target, float scale, Vector2 point)
    {
        target.localScale = new Vector3(scale, scale, 1f);
        target.anchoredPosition = (point - rt.rect.center) * (1f - scale);
    }

}
