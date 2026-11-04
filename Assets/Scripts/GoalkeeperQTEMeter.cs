using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Indicador del QTE del porter en forma de mitja lluna AL VOLTANT DEL PORTER (estil "shot meter" de NBA 2K):
// el jugador no ha d'apartar la vista de la jugada per mirar una barra a la part de baix de la pantalla.
//
//   - L'arc s'obre cap a d'on ve el xut (el porter queda al mig, la bola arriba pel davant).
//   - Una franja crema s'omple d'un extrem a l'altre; la zona verda és on s'ha de prémer.
//   - Al resoldre's, tot l'arc es posa verd (ATURADA!) o vermell (FALLADA!) un moment.
//
// Es crea sol des de GoalkeeperQTE (no cal posar-lo a mà). Va en un Canvas World Space que segueix el porter;
// la mida es manté constant en píxels encara que la càmera faci zoom.
[RequireComponent(typeof(GoalkeeperQTE))]
public class GoalkeeperQTEMeter : MonoBehaviour
{
    [Tooltip("Radi de l'arc en píxels de referència (pantalla de 1080 d'alt)")]
    [SerializeField] float radiusPx = 85f;
    [Tooltip("Gruix de la franja (fracció del radi)")]
    [SerializeField, Range(0.08f, 0.4f)] float thickness = 0.16f;
    [SerializeField] float labelSize = 24f;

    const float CanvasSize = 400f;   // unitats internes del Canvas (es reescala al món)

    GoalkeeperQTE qte;
    Camera cam;
    Canvas canvas;
    RectTransform arc, cursorPivot, label;
    Image track, outline, fill, green, tickBack, tick;
    TextMeshProUGUI labelText;
    CanvasGroup group;
    static Sprite bandSprite, outlineSprite;

    void Awake()
    {
        qte = GetComponent<GoalkeeperQTE>();
        Build();
        group.alpha = 0f;
    }

    void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ---------- Cada frame ----------

    void LateUpdate()
    {
        bool running = qte.State == QTEState.Running;
        bool visible = running || qte.ResultVisible;
        group.alpha = visible ? 1f : 0f;
        if (!visible) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // Segueix el porter; la mida es calcula per tenir sempre els mateixos píxels a pantalla
        Transform root = canvas.transform;
        root.position = new Vector3(transform.position.x, transform.position.y, root.position.z);
        float worldPerPx = cam.orthographic ? cam.orthographicSize * 2f / Screen.height : 0.01f;
        float diameterWorld = radiusPx * 2f * (Screen.height / 1080f) * worldPerPx;
        float s = diameterWorld / (CanvasSize * 0.5f);   // l'arc ocupa la meitat del Canvas
        root.localScale = new Vector3(s, s, 1f);

        if (running)
        {
            float c = Mathf.Clamp01(qte.Cursor01);
            fill.fillAmount = c * 0.5f;
            cursorPivot.localRotation = Quaternion.Euler(0f, 0f, -c * 180f);
            tick.enabled = tickBack.enabled = true;

            bool inside = c >= qte.GreenStart && c <= qte.GreenStart + qte.GreenWidth;
            fill.color = inside ? Color.Lerp(HudSkin.Green, Color.white, 0.45f) : HudSkin.Paper;
            green.color = HudSkin.Green;
            labelText.text = InputMode.IsTouch ? "ATURA!" : "ATURA! [" + qte.KeyName + "]";
            labelText.color = HudSkin.Paper;
        }
        else
        {
            // Veredicte: tot l'arc del color del resultat, sense cursor
            Color c = qte.LastOk ? HudSkin.Green : HudSkin.Red;
            fill.fillAmount = 0.5f;
            fill.color = c;
            green.color = c;
            tick.enabled = tickBack.enabled = false;
            labelText.text = qte.LastOk ? "ATURADA!" : "FALLADA!";
            labelText.color = c;
        }
    }

    // Orienta l'arc cap al xut. El cursor surt sempre de l'extrem de l'esquerra de la pantalla (o del de dalt).
    public void Orient(Vector2 towardShot)
    {
        if (towardShot.sqrMagnitude < 0.0001f) towardShot = Vector2.right;
        towardShot.Normalize();
        float angle = Mathf.Atan2(towardShot.y, towardShot.x) * Mathf.Rad2Deg - 90f;   // "amunt" de l'arc = cap al xut
        arc.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Extrems de l'arc (local -x i +x) en coordenades del món
        Vector2 a = arc.localRotation * Vector3.left, b = arc.localRotation * Vector3.right;
        bool startIsA = a.x < b.x - 0.05f || (Mathf.Abs(a.x - b.x) <= 0.05f && a.y > b.y);
        arc.localScale = new Vector3(startIsA ? 1f : -1f, 1f, 1f);

        // Zona verda: comença a GreenStart del recorregut
        green.fillAmount = qte.GreenWidth * 0.5f;
        green.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -qte.GreenStart * 180f);

        // El text, just per davant de l'arc (cap a d'on ve la bola), sense girar
        float r = CanvasSize * 0.25f;
        label.anchoredPosition = towardShot * (r + 34f);
    }

    // ---------- Construcció ----------

    void Build()
    {
        GameObject go = new GameObject("QTE Meter (" + name + ")", typeof(RectTransform), typeof(Canvas),
                                       typeof(CanvasGroup));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Per damunt de tot el que es dibuixa a la pista
        SortingLayer[] layers = SortingLayer.layers;
        if (layers.Length > 0) canvas.sortingLayerID = layers[layers.Length - 1].id;
        canvas.sortingOrder = 500;
        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        RectTransform root = (RectTransform)go.transform;
        root.sizeDelta = new Vector2(CanvasSize, CanvasSize);
        root.position = new Vector3(transform.position.x, transform.position.y, -1f);

        float d = CanvasSize * 0.5f;   // diàmetre de l'arc en unitats del Canvas

        arc = UIKit.Rect(root, "Arc");
        arc.sizeDelta = new Vector2(d, d);

        if (bandSprite == null) bandSprite = MakeRing(1f - thickness, 1f);
        if (outlineSprite == null) outlineSprite = MakeRing((1f - thickness) * 0.93f, 1f);

        // Contorn fosc (una mica més gran), pista, omplert, zona verda
        outline = HalfRing(arc, "Outline", HudSkin.Ink, outlineSprite, d * 1.07f);
        track = HalfRing(arc, "Track", UIKit.Alpha(HudSkin.Ink, 0.75f), bandSprite, d);
        fill = HalfRing(arc, "Fill", HudSkin.Paper, bandSprite, d);
        green = HalfRing(arc, "Green", HudSkin.Green, bandSprite, d);
        outline.fillAmount = 0.5f;
        track.fillAmount = 0.5f;
        fill.fillAmount = 0f;

        // Cursor: pal radial crema amb contorn fosc a la punta de l'omplert
        cursorPivot = UIKit.Rect(arc, "CursorPivot");
        cursorPivot.sizeDelta = Vector2.zero;
        float rMid = d * 0.5f * (1f - thickness * 0.5f);
        float len = d * 0.5f * thickness * 1.9f;
        tickBack = UIKit.Image(cursorPivot, "TickOutline", HudSkin.Ink, 4f);
        tickBack.rectTransform.sizeDelta = new Vector2(len + 6f, 12f);
        tickBack.rectTransform.anchoredPosition = new Vector2(-rMid, 0f);
        tick = UIKit.Image(cursorPivot, "Tick", Color.white, 3f);
        tick.rectTransform.sizeDelta = new Vector2(len, 6f);
        tick.rectTransform.anchoredPosition = new Vector2(-rMid, 0f);

        // Text (fora de l'arc, perquè no es giri ni s'emmiralli)
        labelText = UIKit.Text(root, "Label", "ATURA!", labelSize, HudSkin.Paper, FontStyles.Bold);
        GameFonts.Style(labelText, GameFonts.Role.Button);
        label = labelText.rectTransform;
        label.sizeDelta = new Vector2(CanvasSize, 50f);
    }

    // Mitja anella omplerta en sentit horari des de l'esquerra (fillAmount 0.5 = tota la meitat de dalt)
    static Image HalfRing(RectTransform parent, string name, Color color, Sprite sprite, float size)
    {
        Image img = UIKit.Image(parent, name, color);
        img.sprite = sprite;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Left;
        img.fillClockwise = true;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    // Anella blanca suavitzada (inner i outer en fracció del radi)
    static Sprite MakeRing(float inner, float outer)
    {
        const int n = 256;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        float c = n * 0.5f, R = c - 1f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(R * outer - r + 0.5f) * Mathf.Clamp01(r - R * inner + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
