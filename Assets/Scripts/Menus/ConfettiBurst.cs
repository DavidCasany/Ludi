using UnityEngine;
using UnityEngine.UI;

// Confeti quan apareix la imatge del torneig: BROLLA de dins la copa (o surt de les dues cantonades de baix),
// s'escampa per sobre del quadre i es queda reposant sobre les caixes dels noms una estona.
// Fet amb Images de UI: un canvas Screen Space - Overlay no dibuixa el ParticleSystem normal.
//
// Per sortir "de dins", la copa es torna a dibuixar PER SOBRE del confeti (cupFront, una capa amb només la copa):
// els trossos neixen dins del bol, amagats, i es veuen quan en surten per la vora.
//
// Totes les coordenades són de la TEXTURA sencera (0..1), encara que el sprite del menú en sigui un retall:
// es tradueixen amb TexturePoint. La física va en "alçades de textura" per segon.
public class ConfettiBurst : MenuPreviewFX
{
    public enum Source { Cup, BottomCorners, Both }

    [Header("D'on surt")]
    [SerializeField] Source source = Source.Cup;
    [Tooltip("Copa dibuixada per sobre del confeti (Image fill d'aquest objecte). Buit = el confeti es veu per davant de la copa")]
    [SerializeField] Image cupFront;
    [Tooltip("Dins del bol: x mín, x màx")]
    [SerializeField] Vector2 mouthX = new Vector2(0.45f, 0.55f);
    [Tooltip("Alçada on neix el confeti, una mica per sota de la vora (0.615) perquè surti de dins")]
    [SerializeField] float mouthY = 0.575f;
    [Tooltip("Cantonades de baix de la imatge (x de l'esquerra; la dreta és 1 - x) i alçada")]
    [SerializeField] Vector2 corner = new Vector2(0.02f, 0.3f);

    [Header("Caixes on es queda (x, y, ample, alt en 0..1 de la textura; compta la vora de dalt)")]
    [SerializeField] Rect[] landBoxes =
    {
        new Rect(0.011f, 0.3757f, 0.104f, 0.0284f), new Rect(0.011f, 0.45f, 0.104f, 0.0278f),
        new Rect(0.011f, 0.522f, 0.104f, 0.0285f),  new Rect(0.011f, 0.5947f, 0.104f, 0.0274f),
        new Rect(0.1675f, 0.4077f, 0.0959f, 0.0288f), new Rect(0.1675f, 0.5557f, 0.0959f, 0.0285f),
        new Rect(0.3113f, 0.4768f, 0.0942f, 0.03f),
        // final: només els trossos que no queden sota la base de la copa
        new Rect(0.4009f, 0.3601f, 0.0436f, 0.0493f), new Rect(0.5549f, 0.3601f, 0.0435f, 0.0493f),
        new Rect(0.5945f, 0.4768f, 0.0947f, 0.03f),
        new Rect(0.7366f, 0.4077f, 0.0959f, 0.0288f), new Rect(0.7366f, 0.5557f, 0.0959f, 0.0285f),
        new Rect(0.885f, 0.3755f, 0.104f, 0.0281f),  new Rect(0.885f, 0.449f, 0.104f, 0.0273f),
        new Rect(0.885f, 0.5215f, 0.104f, 0.0285f),  new Rect(0.885f, 0.595f, 0.104f, 0.0278f),
    };
    [Tooltip("La vora blanca de la caixa sobresurt una mica per sobre del farciment")]
    [SerializeField] float boxTopPadding = 0.005f;
    [Tooltip("Per sota d'aquí, el confeti que no s'ha aturat s'esvaeix")]
    [SerializeField] float fadeBelow = 0.3f;

    [Header("Font (des de la copa)")]
    [SerializeField] int count = 130;
    [SerializeField] float emitDuration = 0.55f;
    [SerializeField] Vector2 speed = new Vector2(0.65f, 1.15f);   // alçades de textura per segon
    [SerializeField, Range(0f, 90f)] float spread = 70f;
    [SerializeField, Range(0f, 1f)] float wideChance = 0.3f;     // part que surt més oberta, per arribar a les puntes
    [SerializeField] Vector2 wideSpeed = new Vector2(0.95f, 1.25f);
    [SerializeField] Vector2 wideAngle = new Vector2(50f, 68f);

    [Header("Canons (des de les cantonades)")]
    [SerializeField] Vector2 cornerSpeed = new Vector2(1.6f, 2.1f);
    [SerializeField] Vector2 cornerAngle = new Vector2(60f, 82f);  // graus des de l'horitzontal, cap al centre

    [Header("Física")]
    [SerializeField] float gravity = 1.5f;
    [SerializeField] float drag = 1.7f;
    [SerializeField] float growTime = 0.1f;

    [Header("Reposar sobre les caixes")]
    [SerializeField] Vector2 restTime = new Vector2(4f, 6f);
    [SerializeField] float restFade = 0.6f;

    [Header("Trossos")]
    [SerializeField] Vector2 size = new Vector2(0.011f, 0.02f);   // ample, en alçades de textura
    [SerializeField] Color[] colors =
    {
        new Color(1f, 0.82f, 0.3f), new Color(0.95f, 0.47f, 0.17f), Color.white,
        new Color(0.3f, 0.6f, 0.95f), new Color(0.9f, 0.27f, 0.32f), new Color(0.35f, 0.8f, 0.45f)
    };

    struct Piece
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos, vel;   // en coordenades de textura (0..1) i textura/segon
        public float born, spin, angle, flip, flipSpeed, sway, swaySpeed, baseSize;
        public bool alive, landed, fromCup;
        public float landedAt, rest;
    }

    Piece[] pieces;
    bool wasVisible;

    protected override void Awake()
    {
        base.Awake();
        pieces = new Piece[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();   // sense sprite = rectangle ple, que és el que volem
            img.raycastTarget = false;
            go.SetActive(false);
            pieces[i] = new Piece { rt = (RectTransform)go.transform, img = img };
        }
        if (cupFront != null) cupFront.transform.SetAsLastSibling();   // la copa tapa el confeti que encara és dins
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (cupFront != null) cupFront.enabled = source != Source.BottomCorners;

        if (!IsVisible)
        {
            if (wasVisible) StopAll();
            wasVisible = false;
            return;
        }
        if (!wasVisible) { wasVisible = true; Burst(); }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float now = Time.unscaledTime;
        float px = TextureHeightToLocal();   // píxels locals per alçada de textura
        Vector2 center = rt.rect.center;

        for (int i = 0; i < pieces.Length; i++)
        {
            ref Piece p = ref pieces[i];
            if (!p.alive || now < p.born) continue;
            if (!p.rt.gameObject.activeSelf) p.rt.gameObject.SetActive(true);

            float alpha = 1f;
            if (p.landed)
            {
                float t = now - p.landedAt - p.rest;
                if (t > 0f) alpha = 1f - t / restFade;
                if (alpha <= 0f) { Kill(ref p); continue; }
            }
            else
            {
                Vector2 before = p.pos;
                p.vel.y -= gravity * dt;
                p.vel -= p.vel * Mathf.Min(1f, drag * dt);
                p.pos += p.vel * dt;
                p.angle += p.spin * dt;
                p.flip += p.flipSpeed * dt;
                p.sway += p.swaySpeed * dt;

                if (p.vel.y < 0f && TryLand(before, p.pos, out float top))
                {
                    p.landed = true;
                    p.landedAt = now;
                    p.rest = Random.Range(restTime.x, restTime.y);
                    p.pos.y = top;
                    p.vel = Vector2.zero;
                    p.angle = Random.Range(-20f, 20f);   // queda estirat sobre la caixa
                }
                else
                {
                    alpha = Mathf.Clamp01((p.pos.y - (fadeBelow - 0.06f)) / 0.06f);
                    if (alpha <= 0f) { Kill(ref p); continue; }
                }
            }

            // Mentre baixa es balanceja; quiet sobre la caixa, no (si no, semblaria que sura)
            float swayX = p.landed ? 0f : Mathf.Sin(p.sway) * 0.008f;
            p.rt.anchoredPosition = TexturePoint(new Vector2(p.pos.x + swayX, p.pos.y)) - center;
            p.rt.localRotation = Quaternion.Euler(0f, 0f, p.angle);

            float grow = Mathf.Clamp01((now - p.born) / Mathf.Max(0.01f, growTime));
            float flipX = p.landed ? 1f : Mathf.Cos(p.flip);
            float w = p.baseSize * px;
            p.rt.sizeDelta = new Vector2(w, w * 0.55f);
            p.rt.localScale = new Vector3(flipX * grow, grow, 1f);

            Color c = p.img.color;
            c.a = alpha;
            p.img.color = c;
        }
    }

    // Ha creuat (baixant) la vora de dalt d'alguna caixa en aquest pas?
    bool TryLand(Vector2 from, Vector2 to, out float top)
    {
        top = 0f;
        for (int i = 0; i < landBoxes.Length; i++)
        {
            Rect b = landBoxes[i];
            float t = b.yMax + boxTopPadding;
            if (from.y >= t && to.y < t && to.x >= b.xMin && to.x <= b.xMax)
            {
                top = t;
                return true;
            }
        }
        return false;
    }

    void Burst()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < pieces.Length; i++)
        {
            ref Piece p = ref pieces[i];
            bool fromCup = source == Source.Cup || (source == Source.Both && i % 2 == 0);

            if (fromCup)
            {
                float u = Random.Range(mouthX.x, mouthX.y);
                float side = Mathf.InverseLerp(mouthX.x, mouthX.y, u) * 2f - 1f;   // com més a la vora, més obert
                float ang, spd;
                if (Random.value < wideChance)
                {
                    float dir = side >= 0f ? 1f : -1f;
                    ang = 90f - dir * Random.Range(wideAngle.x, wideAngle.y);
                    spd = Random.Range(wideSpeed.x, wideSpeed.y);
                }
                else
                {
                    ang = 90f - side * spread * 0.6f + Random.Range(-spread * 0.4f, spread * 0.4f);
                    spd = Random.Range(speed.x, speed.y);
                }
                p.pos = new Vector2(u, mouthY);
                p.vel = Dir(ang) * spd;
                p.born = now + Random.Range(0f, emitDuration);
            }
            else
            {
                bool left = (i / 2) % 2 == 0;
                float ang = Random.Range(cornerAngle.x, cornerAngle.y);
                if (!left) ang = 180f - ang;
                p.pos = new Vector2(left ? corner.x : 1f - corner.x, corner.y);
                p.vel = Dir(ang) * Random.Range(cornerSpeed.x, cornerSpeed.y);
                p.born = now + Random.Range(0f, emitDuration * 0.6f);   // canonada: més de cop
            }

            p.fromCup = fromCup;
            p.spin = Random.Range(-360f, 360f);
            p.angle = Random.Range(0f, 360f);
            p.flip = Random.Range(0f, Mathf.PI * 2f);
            p.flipSpeed = Random.Range(6f, 14f);
            p.sway = Random.Range(0f, Mathf.PI * 2f);
            p.swaySpeed = Random.Range(2f, 5f);
            p.baseSize = Random.Range(size.x, size.y);
            p.alive = true;
            p.landed = false;
            p.img.color = colors.Length > 0 ? colors[Random.Range(0, colors.Length)] : Color.white;
            p.rt.localScale = Vector3.zero;
            p.rt.gameObject.SetActive(false);   // s'encén quan li toca sortir (born)
        }
    }

    static Vector2 Dir(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    void StopAll()
    {
        for (int i = 0; i < pieces.Length; i++) Kill(ref pieces[i]);
    }

    static void Kill(ref Piece p)
    {
        p.alive = false;
        p.landed = false;
        if (p.rt != null) p.rt.gameObject.SetActive(false);
    }
}
