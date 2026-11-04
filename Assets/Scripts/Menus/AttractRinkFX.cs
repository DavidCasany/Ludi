using UnityEngine;
using UnityEngine.UI;

// Escena viva per a la imatge per defecte del menú (sobre el fons de la pista, menu_rink.png):
// dos companys patinen i es passen la bola i un rival la persegueix per tallar-la.
//  - Els jugadors són les mateixes capes que al partit (Assets/Sprites/Player/), pintades amb TeamData reals.
//  - Cada pocs pases els equips canvien (dos equips a l'atzar, amb els seus colors i patrons).
//  - L'anell neó va sota de qui té la bola.
// Tot és UI i temps real: funciona amb el canvas Screen Space - Overlay i amb el temps aturat.
// Coordenades internes en "alçades d'imatge": x de 0 a aspect, y de 0 a 1 (aspect = ample / alt de la pista).
public class AttractRinkFX : MenuPreviewFX
{
    [Header("Sprites (els posa Tools > Ludi > Afegir animacions al menú d'inici)")]
    [SerializeField] Sprite shadowSprite;
    [SerializeField] Sprite baseSprite;
    [SerializeField] Sprite jerseySprite;
    [SerializeField] Sprite headSprite;
    [SerializeField] Sprite stickSprite;
    [SerializeField] Sprite glovesSprite;
    [SerializeField] Sprite ballSprite;
    [SerializeField] Sprite ringSprite;

    [Header("Equips")]
    [SerializeField] TeamData[] teams;
    [SerializeField] int passesPerTeamChange = 4;

    [Header("Mides (alçades d'imatge)")]
    [SerializeField] float playerSize = 0.2f;     // mida del llenç del jugador (el tors en fa ~39%)
    [SerializeField] float ballSize = 0.028f;
    [Tooltip("Angle de la pala dins del sprite de l'estic (graus des de +X) i distància al centre (fracció del llenç)")]
    [SerializeField] float bladeAngle = 64f;
    [SerializeField] float bladeDistance = 0.5f;

    [Header("Moviment")]
    [SerializeField] float skateSpeed = 0.22f;
    [SerializeField] float rivalSpeed = 0.17f;
    [SerializeField] float acceleration = 0.5f;
    [SerializeField] float passSpeed = 0.95f;
    [SerializeField] Vector2 holdTime = new Vector2(1.1f, 1.9f);
    [SerializeField] Color ringColor = new Color(0.35f, 0.85f, 1f, 0.9f);

    class Player
    {
        public RectTransform root, body, stick, gloves;
        public Image jersey, pattern, glovesImg;
        public Vector2 pos, vel, anchor;
        public float bodyAngle = 90f, stickAngle = 90f, seed;
    }

    Player[] players;               // 0 i 1 = companys, 2 = rival
    RectTransform ball, ring, playersLayer;
    Image ringImg;
    CanvasGroup playersGroup;

    Vector2 ballPos;
    int carrier = 0, receiver = -1;
    float holdUntil;
    int passes;
    float swapT = -1f;               // >= 0 mentre canvien els equips (fos)
    bool wasVisible;
    float aspect = 4f / 3f;

    // ---------- Construcció ----------

    protected override void Awake()
    {
        base.Awake();

        ring = NewRect("Ring", transform);
        ringImg = ring.gameObject.AddComponent<Image>();
        ringImg.sprite = ringSprite;
        ringImg.raycastTarget = false;
        ringImg.enabled = ringSprite != null;

        ball = NewRect("Ball", transform);
        Image b = ball.gameObject.AddComponent<Image>();
        b.sprite = ballSprite;
        b.raycastTarget = false;
        b.enabled = ballSprite != null;

        playersLayer = NewRect("Players", transform);
        Stretch(playersLayer);
        playersGroup = playersLayer.gameObject.AddComponent<CanvasGroup>();
        playersGroup.blocksRaycasts = false;

        players = new Player[3];
        for (int i = 0; i < 3; i++) players[i] = BuildPlayer(i);
    }

    Player BuildPlayer(int i)
    {
        Player p = new Player { seed = i * 17.3f + 3.1f };
        p.root = NewRect("Player" + i, playersLayer);
        p.body = NewRect("Body", p.root);
        Stretch(p.body);
        Layer(p.body, shadowSprite);
        Layer(p.body, baseSprite);
        p.jersey = Layer(p.body, jerseySprite);
        p.pattern = Layer(p.body, null);
        p.stick = NewRect("StickPivot", p.body);
        Stretch(p.stick);
        Layer(p.stick, stickSprite);
        Layer(p.body, headSprite);
        p.gloves = NewRect("GlovesPivot", p.body);
        Stretch(p.gloves);
        p.glovesImg = Layer(p.gloves, glovesSprite);
        return p;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Image Layer(Transform parent, Sprite s)
    {
        RectTransform rt = NewRect("Layer", parent);
        Stretch(rt);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = s != null;   // sense sprite es veuria un quadrat blanc
        return img;
    }

    // ---------- Escena ----------

    void ResetScene()
    {
        aspect = forSprite != null ? forSprite.rect.width / forSprite.rect.height : 4f / 3f;
        Vector2[] anchors =
        {
            new Vector2(aspect * 0.32f, 0.38f), new Vector2(aspect * 0.66f, 0.62f), new Vector2(aspect * 0.52f, 0.5f)
        };
        for (int i = 0; i < players.Length; i++)
        {
            players[i].anchor = anchors[i];
            players[i].pos = anchors[i];
            players[i].vel = Vector2.zero;
        }
        carrier = 0;
        receiver = -1;
        holdUntil = Time.unscaledTime + 0.8f;
        passes = 0;
        ApplyRandomTeams();
    }

    void ApplyRandomTeams()
    {
        if (teams == null || teams.Length == 0) return;
        TeamData a = teams[Random.Range(0, teams.Length)];
        TeamData b = a;
        for (int k = 0; k < 10 && (b == a || b == null); k++) b = teams[Random.Range(0, teams.Length)];
        if (a == null || b == null) return;

        // Com al partit: si els colors s'assemblen massa, el rival es posa la segona equipació
        bool clash = ColorDistance(a.PrimaryColor, b.PrimaryColor) < 0.3f;
        Kit(players[0], a.PrimaryColor, a.SecondaryColor, a.JerseyPattern);
        Kit(players[1], a.PrimaryColor, a.SecondaryColor, a.JerseyPattern);
        if (clash) Kit(players[2], b.AlternateColor, b.SecondaryColor, b.AlternatePattern);
        else Kit(players[2], b.PrimaryColor, b.SecondaryColor, b.JerseyPattern);
    }

    static void Kit(Player p, Color shirt, Color trim, Sprite pattern)
    {
        p.jersey.color = shirt;
        p.glovesImg.color = trim;
        p.pattern.sprite = pattern;
        p.pattern.color = trim;
        p.pattern.enabled = pattern != null;
    }

    static float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return Mathf.Sqrt(0.3f * dr * dr + 0.59f * dg * dg + 0.11f * db * db);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (!IsVisible) { wasVisible = false; return; }
        if (!wasVisible) { wasVisible = true; ResetScene(); }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;

        MovePlayers(dt, now);
        UpdateBall(dt, now);
        UpdateSwap(dt);
        Draw(now);
    }

    void MovePlayers(float dt, float now)
    {
        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            Vector2 target;
            float speed = skateSpeed;

            if (i == 2)
            {
                // Rival: va a buscar la bola, però no s'enganxa a qui la porta
                target = ballPos;
                speed = rivalSpeed;
                if (carrier >= 0)
                {
                    Vector2 away = p.pos - players[carrier].pos;
                    if (away.magnitude < 0.12f) target = players[carrier].pos + away.normalized * 0.12f;
                }
            }
            else
            {
                // Companys: patinen pel voltant de la seva zona (soroll suau = moviment natural, no en cercles)
                float t = now * 0.22f;
                Vector2 wander = new Vector2(Mathf.PerlinNoise(t, p.seed) - 0.5f, Mathf.PerlinNoise(p.seed, t) - 0.5f);
                target = p.anchor + new Vector2(wander.x * aspect * 0.45f, wander.y * 0.4f);
                if (i == carrier) speed *= 1.15f;
            }

            target.x = Mathf.Clamp(target.x, 0.17f, aspect - 0.17f);
            target.y = Mathf.Clamp(target.y, 0.16f, 0.84f);

            Vector2 desired = Vector2.ClampMagnitude((target - p.pos) * 2.5f, speed);
            p.vel = Vector2.MoveTowards(p.vel, desired, acceleration * dt);   // inèrcia de patí
            p.pos += p.vel * dt;
        }
    }

    Vector2 BladePoint(Player p)
    {
        float a = p.stickAngle * Mathf.Deg2Rad;
        return p.pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * bladeDistance * playerSize;
    }

    void UpdateBall(float dt, float now)
    {
        if (carrier >= 0)
        {
            Player c = players[carrier];
            ballPos = BladePoint(c);

            if (now >= holdUntil)
            {
                receiver = 1 - carrier;
                carrier = -1;
            }
        }
        else if (receiver >= 0)
        {
            Player r = players[receiver];
            Vector2 target = BladePoint(r);
            ballPos = Vector2.MoveTowards(ballPos, target, passSpeed * dt);
            if (Vector2.Distance(ballPos, target) < 0.01f)
            {
                carrier = receiver;
                receiver = -1;
                holdUntil = now + Random.Range(holdTime.x, holdTime.y);
                passes++;
                if (passesPerTeamChange > 0 && passes % passesPerTeamChange == 0) swapT = 0f;
            }
        }
    }

    // Canvi d'equips: els jugadors s'esvaeixen, canvien de samarreta i tornen
    void UpdateSwap(float dt)
    {
        if (swapT < 0f) { playersGroup.alpha = 1f; return; }
        float before = swapT;
        swapT += dt / 0.6f;
        if (before < 0.5f && swapT >= 0.5f) ApplyRandomTeams();
        playersGroup.alpha = Mathf.Abs(swapT - 0.5f) * 2f;
        if (swapT >= 1f) { swapT = -1f; playersGroup.alpha = 1f; }
    }

    Vector2 ToLocal(Rect d, Vector2 q) => new Vector2(d.x + q.x * d.height, d.y + q.y * d.height) - rt.rect.center;

    void Draw(float now)
    {
        Rect d = DrawnRect();
        float h = d.height;

        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];

            // Cos: on va si patina ràpid; si no, cap a la bola. L'estic sempre busca la bola.
            Vector2 look = p.vel.magnitude > 0.06f ? p.vel : ballPos - p.pos;
            if (i == carrier && receiver < 0 && holdUntil - now < 0.35f)
                look = players[1 - i].pos - p.pos;   // a punt de passar: mira el company
            float bodyTarget = Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg;
            p.bodyAngle = Mathf.MoveTowardsAngle(p.bodyAngle, bodyTarget, 400f * Time.unscaledDeltaTime);

            Vector2 toBall = ballPos - p.pos;
            float ballAngle = Mathf.Atan2(toBall.y, toBall.x) * Mathf.Rad2Deg;
            // Qui porta la bola la duu una mica a la dreta del davant (la bola va on és la pala, no al revés)
            if (i == carrier) ballAngle = p.bodyAngle - 15f;
            float stickTarget = p.bodyAngle + Mathf.Clamp(Mathf.DeltaAngle(p.bodyAngle, ballAngle), -110f, 110f);
            p.stickAngle = Mathf.MoveTowardsAngle(p.stickAngle, stickTarget, 600f * Time.unscaledDeltaTime);

            p.root.sizeDelta = Vector2.one * playerSize * h;
            p.root.anchoredPosition = ToLocal(d, p.pos);
            p.body.localRotation = Quaternion.Euler(0f, 0f, p.bodyAngle - 90f);   // els sprites miren amunt
            Quaternion stickRot = Quaternion.Euler(0f, 0f, p.stickAngle - p.bodyAngle - (bladeAngle - 90f));
            p.stick.localRotation = stickRot;
            p.gloves.localRotation = stickRot;
        }

        // Ordre de dibuix: el que és més avall a la pista va per sobre (3 jugadors: n'hi ha prou amb comparar)
        for (int rank = 0; rank < players.Length; rank++)
            for (int i = 0; i < players.Length; i++)
            {
                int above = 0;
                for (int j = 0; j < players.Length; j++)
                    if (j != i && (players[j].pos.y > players[i].pos.y || (players[j].pos.y == players[i].pos.y && j < i))) above++;
                if (above == rank) players[i].root.SetSiblingIndex(rank);   // de rang 0 cap amunt: ordre correcte
            }

        ball.sizeDelta = Vector2.one * ballSize * h;
        ball.anchoredPosition = ToLocal(d, ballPos);

        // Anell neó sota qui té (o rebrà) la bola
        int owner = carrier >= 0 ? carrier : receiver;
        if (owner >= 0)
        {
            ring.sizeDelta = Vector2.one * playerSize * 0.62f * h;
            ring.anchoredPosition = ToLocal(d, players[owner].pos);
            Color c = ringColor;
            c.a *= (carrier >= 0 ? 1f : 0.35f) * (0.75f + 0.25f * Mathf.Sin(now * 3f));
            ringImg.color = c;
        }
    }
}
