using UnityEngine;
using UnityEngine.UI;

// Confeti que surt disparat de la copa quan apareix la imatge del torneig i cau per sobre dels quadres
// del quadre. Fet amb Images de UI (un canvas Screen Space - Overlay no dibuixa el ParticleSystem normal).
// Cada tros: impuls cap amunt i enfora, gravetat, frec de l'aire (cau a poc a poc), balanceig lateral,
// gir i un "volteig" (escala X que va de 1 a -1) perquè sembli paper. S'esvaeix en passar el quadre.
// Totes les mides i velocitats són en proporció a l'alçada de la imatge: funciona a qualsevol resolució.
public class ConfettiBurst : MenuPreviewFX
{
    [Header("Origen i recorregut (coordenades 0..1 de la imatge)")]
    [SerializeField] Vector2 origin = new Vector2(0.5f, 0.6f);     // boca de la copa
    [SerializeField] float fadeBelow = 0.3f;                        // per sota del quadre s'esvaeix

    [Header("Explosió")]
    [SerializeField] int count = 90;
    [SerializeField] float emitDuration = 0.15f;
    [SerializeField] Vector2 speed = new Vector2(0.7f, 1.4f);       // alçades d'imatge per segon
    [SerializeField, Range(0f, 90f)] float spread = 70f;            // graus a cada costat de la vertical
    [SerializeField] float gravity = 1.3f;
    [SerializeField] float drag = 2.2f;

    [Header("Trossos")]
    [SerializeField] Vector2 size = new Vector2(0.012f, 0.022f);    // ample, en alçades d'imatge
    [SerializeField] Color[] colors =
    {
        new Color(1f, 0.82f, 0.3f), new Color(0.95f, 0.47f, 0.17f), Color.white,
        new Color(0.3f, 0.6f, 0.95f), new Color(0.9f, 0.27f, 0.32f), new Color(0.35f, 0.8f, 0.45f)
    };

    struct Piece
    {
        public RectTransform rt;
        public Image img;
        public Vector2 pos, vel;
        public float born, spin, angle, flip, flipSpeed, sway, swaySpeed;
        public bool alive;
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
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (!IsVisible)
        {
            if (wasVisible) StopAll();
            wasVisible = false;
            return;
        }
        if (!wasVisible) { wasVisible = true; Burst(); }

        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;
        Rect d = DrawnRect();
        float h = d.height;

        for (int i = 0; i < pieces.Length; i++)
        {
            ref Piece p = ref pieces[i];
            if (!p.alive || now < p.born) continue;
            if (!p.rt.gameObject.activeSelf) p.rt.gameObject.SetActive(true);

            // Física en "alçades d'imatge": gravetat + frec (velocitat límit = gravity / drag)
            p.vel.y -= gravity * dt;
            p.vel -= p.vel * Mathf.Min(1f, drag * dt);
            p.pos += p.vel * dt;
            p.angle += p.spin * dt;
            p.flip += p.flipSpeed * dt;
            p.sway += p.swaySpeed * dt;

            float swayX = Mathf.Sin(p.sway) * 0.012f;   // balanceig de paper caient
            // (x en alçades des del centre de la imatge) -> coordenades locals -> relatives al centre (ancoratge)
            Vector2 local = new Vector2(d.center.x + (p.pos.x + swayX - 0.5f) * h, d.y + p.pos.y * h);
            p.rt.anchoredPosition = local - rt.rect.center;
            p.rt.localRotation = Quaternion.Euler(0f, 0f, p.angle);
            p.rt.localScale = new Vector3(Mathf.Cos(p.flip), 1f, 1f);

            // S'esvaeix en passar per sota del quadre
            float fade = Mathf.Clamp01((p.pos.y - (fadeBelow - 0.08f)) / 0.08f);
            Color c = p.img.color;
            c.a = fade;
            p.img.color = c;
            if (fade <= 0f) Kill(ref p);
        }
    }

    void Burst()
    {
        Rect d = DrawnRect();
        float h = Mathf.Max(1f, d.height);
        float now = Time.unscaledTime;

        // Posicions en "alçades d'imatge", amb x mesurada des del centre de la imatge (0.5 = centre)
        float ox = 0.5f + (origin.x - 0.5f) * d.width / h;
        for (int i = 0; i < pieces.Length; i++)
        {
            ref Piece p = ref pieces[i];
            float ang = (90f + Random.Range(-spread, spread)) * Mathf.Deg2Rad;
            float spd = Random.Range(speed.x, speed.y);
            p.pos = new Vector2(ox + Random.Range(-0.02f, 0.02f), origin.y);
            p.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            p.born = now + Random.Range(0f, emitDuration);
            p.spin = Random.Range(-360f, 360f);
            p.angle = Random.Range(0f, 360f);
            p.flip = Random.Range(0f, Mathf.PI * 2f);
            p.flipSpeed = Random.Range(6f, 14f);
            p.sway = Random.Range(0f, Mathf.PI * 2f);
            p.swaySpeed = Random.Range(2f, 5f);
            p.alive = true;

            float w = Random.Range(size.x, size.y) * h;
            p.rt.sizeDelta = new Vector2(w, w * Random.Range(0.45f, 0.7f));
            p.img.color = colors.Length > 0 ? colors[Random.Range(0, colors.Length)] : Color.white;
            p.rt.gameObject.SetActive(false);   // s'encén quan li toca sortir (born)
        }
    }

    void StopAll()
    {
        for (int i = 0; i < pieces.Length; i++) Kill(ref pieces[i]);
    }

    static void Kill(ref Piece p)
    {
        p.alive = false;
        if (p.rt != null) p.rt.gameObject.SetActive(false);
    }
}
