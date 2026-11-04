using UnityEngine;
using UnityEngine.Rendering;

// "PORTER EN RATXA": si un porter encaixa molts gols en una mateixa part, s'escalfa i durant la MEITAT del
// temps que queda de la part para millor (reacciona abans, llegeix millor el xut, s'estira més ràpid i més
// lluny; si és el teu porter, la zona verda del QTE és més ampla). Després torna al seu nivell normal.
// Així no es poden fer 15 gols en 2 minuts, però marcar continua sent possible.
//
// Val per als dos porters (el teu i el de la IA). Es crea sol des de Goalkeeper: no cal posar-lo a mà,
// però si el poses al prefab pots retocar els valors a l'Inspector.
// El temps es compta amb el rellotge del partit: durant les pauses de gol el rellotge s'atura i la ratxa també.
[RequireComponent(typeof(Goalkeeper))]
public class GoalkeeperStreak : MonoBehaviour
{
    [Tooltip("Gols encaixats en una mateixa part per activar la ratxa")]
    [SerializeField] int goalsToTrigger = 3;
    [Tooltip("Fracció del temps que queda de la part que dura la ratxa (0.5 = la meitat)")]
    [SerializeField, Range(0.1f, 1f)] float durationFraction = 0.5f;
    [Tooltip("Durada mínima en segons de rellotge (si queda poc temps de part)")]
    [SerializeField] float minDuration = 15f;
    [Tooltip("Segons reals que tarda a arribar al màxim i a tornar a la normalitat")]
    [SerializeField] float rampTime = 1.2f;

    [Header("Aura")]
    [SerializeField] Color auraColor = new Color(1f, 0.78f, 0.25f, 0.85f);
    [SerializeField] float auraSize = 2.2f;   // diàmetre en unitats del món

    public bool Active { get; private set; }

    Goalkeeper keeper;
    GameManager gm;
    int conceded, half = -1;
    float endTimeLeft;     // la ratxa s'acaba quan el rellotge de la part arriba aquí
    float level;           // 0..1, el que fa servir Goalkeeper (puja i baixa suau)
    SpriteRenderer aura;
    static Sprite auraSprite;

    void Awake()
    {
        keeper = GetComponent<Goalkeeper>();
        BuildAura();
    }

    void OnEnable() => TrySubscribe();

    void OnDisable()
    {
        if (gm != null) gm.GoalScoredBy -= OnGoal;
        gm = null;
    }

    void TrySubscribe()
    {
        if (gm != null) return;
        gm = GameManager.Instance;
        if (gm != null) gm.GoalScoredBy += OnGoal;
    }

    void OnGoal(Team scorer)
    {
        if (scorer == keeper.team || gm.IsGoldenGoal) return;   // gol a favor o gol d'or: res
        CheckHalf();
        conceded++;
        if (Active || conceded < goalsToTrigger) return;

        float duration = Mathf.Max(minDuration, gm.TimeLeft * durationFraction);
        endTimeLeft = Mathf.Max(0f, gm.TimeLeft - duration);
        Active = true;
        conceded = 0;   // per tornar-la a activar caldran uns altres 'goalsToTrigger' gols
        Feedback.Popup(transform.position, "PORTER EN RATXA!", auraColor, 1.6f);
    }

    // Canvi de part: es comença de zero
    void CheckHalf()
    {
        if (gm.Half == half) return;
        half = gm.Half;
        conceded = 0;
        Active = false;
    }

    void Update()
    {
        if (gm == null) TrySubscribe();
        if (gm == null) return;
        CheckHalf();

        if (Active && (gm.TimeLeft <= endTimeLeft || gm.IsGoldenGoal || gm.State == MatchState.FullTime))
            Active = false;

        float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, rampTime);
        level = Mathf.MoveTowards(level, Active ? 1f : 0f, step);
        keeper.Boost = level;

        // Aura que batega sota el porter mentre dura la ratxa
        if (aura != null)
        {
            aura.enabled = level > 0.01f;
            if (aura.enabled)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                Color c = auraColor;
                c.a *= level * pulse;
                aura.color = c;
                float s = auraSize * (0.92f + 0.08f * pulse);
                aura.transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    // ---------- Aura ----------

    void BuildAura()
    {
        GameObject go = new GameObject("StreakAura");
        go.transform.SetParent(transform, false);
        aura = go.AddComponent<SpriteRenderer>();
        if (auraSprite == null) auraSprite = MakeGlow();
        aura.sprite = auraSprite;
        aura.enabled = false;

        // Just per sota del porter, a la seva mateixa capa (el cos va dins d'un SortingGroup)
        SpriteRenderer any = null;
        foreach (SpriteRenderer r in GetComponentsInChildren<SpriteRenderer>(true))
            if (r != aura) { any = r; break; }
        if (any != null) aura.sharedMaterial = any.sharedMaterial;

        SortingGroup group = GetComponentInChildren<SortingGroup>(true);
        if (group != null)
        {
            aura.sortingLayerID = group.sortingLayerID;
            aura.sortingOrder = group.sortingOrder - 1;
        }
        else if (any != null)
        {
            aura.sortingLayerID = any.sortingLayerID;
            aura.sortingOrder = any.sortingOrder - 5;
        }
    }

    // Anell suau (gruixut al mig, s'esvaeix cap a dins i cap a fora), mida 1 unitat del món
    static Sprite MakeGlow()
    {
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        float c = n * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) / c;   // 0..1
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.1f, 2f));   // anell brillant
                float glow = Mathf.Clamp01(1f - r) * 0.25f;                     // una mica de llum a dins
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + glow) * (r < 1f ? 1f : 0f)));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }
}
