using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pantalla "CONTROLS": el manual de com es juga, amb el dibuix de cada tecla i el que fa.
// Es construeix per codi en el seu propi Canvas (per sobre del menú de pausa i de la confirmació),
// igual que ConfirmDialog, així que funciona des de qualsevol escena:
//   ControlsScreen.Show();                         // des del menú d'inici
//   ControlsScreen.Show(() => Select(elMeuBoto));  // amb una acció en tancar
// Dues pestanyes: TECLAT I RATOLÍ / PANTALLA TÀCTIL (per defecte la del dispositiu que s'està fent servir).
// Esc o el botó B del mando la tanquen. Funciona amb Time.timeScale = 0 (tot va amb temps real).
//
// Dibuixos: Assets/Resources/Art/Controls/ (tecles i ratolí, fets amb l'estètica de l'art del joc).
// Si falten, les tecles es dibuixen amb rectangles arrodonits i el joc no peta.
public class ControlsScreen : MonoBehaviour
{
    public static bool IsOpen => current != null;

    // Esc tanca aquesta pantalla i PauseController llegeix el mateix Esc en el mateix frame:
    // així PauseMenuUI sap que la tecla ja s'ha fet servir i no reprèn el partit.
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;

    static ControlsScreen current;
    static int closedFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
        closedFrame = -1;
    }

    // ---------- Mides (píxels de referència, 1920×1080) ----------

    const float CardW = 1680f, CardH = 940f;
    const float BodyTop = 190f, BodyBottom = 130f, BodySide = 50f;
    const float ColumnGap = 30f, SectionGap = 24f;
    const float HeaderH = 58f, RowH = 104f, NoteH = 64f, SectionPad = 16f;
    const float IconW = 184f;

    const string ArtFolder = "Art/Controls/";

    // Colors dels botons tàctils (els mateixos que TouchControls, però opacs per veure's sobre el paper)
    static readonly Color TouchShoot = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color TouchNavy = new Color(0.16f, 0.21f, 0.28f);
    static readonly Color TouchSprint = new Color(0.2f, 0.45f, 0.3f);
    static readonly Color TouchQte = new Color(0.25f, 0.8f, 0.38f);

    Action onClose;
    RectTransform card, body, tabs;
    Button keyboardTab, touchTab, backButton;
    bool touchPage;
    float openedAt;
    readonly List<Graphic> pulsing = new List<Graphic>();

    // ---------- Obrir / tancar ----------

    public static ControlsScreen Show(Action onClose = null)
    {
        if (current != null) return current;

        Canvas canvas = UIKit.CreateCanvas("ControlsScreen", 600);
        ControlsScreen s = canvas.gameObject.AddComponent<ControlsScreen>();
        s.onClose = onClose;
        s.touchPage = InputMode.IsTouch;
        s.Build((RectTransform)canvas.transform);
        current = s;
        return s;
    }

    // Tanca sense cridar l'acció (p. ex. si es reprèn el partit amb la pantalla oberta)
    public static void Hide()
    {
        if (current == null) return;
        current.onClose = null;
        current.Close();
    }

    public void Close()
    {
        if (current == this) current = null;
        closedFrame = Time.frameCount;
        Action action = onClose;
        onClose = null;
        Destroy(gameObject);
        action?.Invoke();
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    void Update()
    {
        if (UIKit.BackPressed())
        {
            Close();
            return;
        }

        // Entrada: petit "pop" de la targeta (temps real: en pausa Time.timeScale = 0)
        float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.18f);
        float s = Mathf.Lerp(0.94f, 1f, 1f - (1f - t) * (1f - t));
        card.localScale = new Vector3(s, s, 1f);

        // Les etiquetes "MANTÉN" bateguen: és una acció que es fa mantenint premut
        float a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
        for (int i = pulsing.Count - 1; i >= 0; i--)
        {
            if (pulsing[i] == null) { pulsing.RemoveAt(i); continue; }
            Color c = pulsing[i].color;
            c.a = a;
            pulsing[i].color = c;
        }
    }

    // ---------- Construcció ----------

    void Build(RectTransform root)
    {
        openedAt = Time.unscaledTime;

        Image dim = UIKit.Image(root, "Dim", UIKit.Alpha(Color.black, 0.75f), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);

        Image cardImg = UIKit.SteelPanel(root, "Card", 40f, true);
        card = cardImg.rectTransform;
        UIKit.Place(card, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));

        Image bar = UIKit.Image(card, "Accent", UIKit.Accent, 3f);
        UIKit.Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -44f), new Vector2(110f, 8f));

        TextMeshProUGUI title = UIKit.Text(card, "Title", "CONTROLS", 72f, UIKit.TextMain, FontStyles.Bold,
                                           TextAlignmentOptions.Left);
        UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -58f), new Vector2(760f, 84f));

        TextMeshProUGUI sub = UIKit.Text(card, "Subtitle", "Com es juga a Hockey Maniacs", 26f, UIKit.TextSoft,
                                         FontStyles.Normal, TextAlignmentOptions.Left);
        UIKit.Place(sub.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -140f), new Vector2(760f, 36f));

        tabs = UIKit.Rect(card, "Tabs");
        UIKit.Place(tabs, new Vector2(1f, 1f), new Vector2(-60f, -62f), new Vector2(700f, 76f));

        body = UIKit.Rect(card, "Body");
        UIKit.Stretch(body, BodySide, BodyBottom, BodySide, BodyTop);

        backButton = UIKit.Button(card, "Back", "TORNAR", true, 32f);
        UIKit.Place((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f),
                    new Vector2(340f, 84f));
        backButton.onClick.AddListener(Close);

        ShowPage(touchPage, false);
        UIKit.Select(backButton);
    }

    void ShowPage(bool touch, bool selectTab)
    {
        touchPage = touch;
        BuildTabs();
        pulsing.Clear();
        for (int i = body.childCount - 1; i >= 0; i--) Destroy(body.GetChild(i).gameObject);

        if (touch) BuildTouchPage();
        else BuildKeyboardPage();

        if (selectTab) UIKit.Select(touch ? touchTab : keyboardTab);
    }

    void BuildTabs()
    {
        for (int i = tabs.childCount - 1; i >= 0; i--) Destroy(tabs.GetChild(i).gameObject);

        // La pestanya activa és la "principal" (taronja)
        keyboardTab = UIKit.Button(tabs, "TabKeyboard", "TECLAT I RATOLÍ", !touchPage, 26f);
        UIKit.Place((RectTransform)keyboardTab.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(380f, 76f));
        keyboardTab.onClick.AddListener(() => { if (touchPage) ShowPage(false, true); });

        touchTab = UIKit.Button(tabs, "TabTouch", "TÀCTIL", touchPage, 26f);
        UIKit.Place((RectTransform)touchTab.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(300f, 76f));
        touchTab.onClick.AddListener(() => { if (!touchPage) ShowPage(true, true); });

        keyboardTab.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit, selectOnRight = touchTab, selectOnDown = backButton
        };
        touchTab.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit, selectOnLeft = keyboardTab, selectOnDown = backButton
        };
        backButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit, selectOnUp = touchPage ? touchTab : keyboardTab
        };
    }

    // ---------- Pàgines ----------

    void BuildKeyboardPage()
    {
        RectTransform c0 = Column(0), c1 = Column(1), c2 = Column(2);

        float y = Section(c0, 0f, "MOVIMENT",
            Row(r => KeyWASD(r), "PATINAR", "Mou el jugador en totes les direccions."),
            Row(r => Key(r, "SHIFT", KeySize.Wide, 70f), "ESPRINTAR", "Mantén per córrer més. Gasta la barra."),
            Row(r => Mouse(r, null, null), "APUNTAR", "El ratolí marca on xutes i on passes."));
        Section(c0, y, "GENERAL",
            Row(r => KeyPair(r, "ESC", "P"), "PAUSA", "Atura el partit i obre el menú."));

        Section(c1, 0f, "AMB LA BOLA",
            Row(r => Mouse(r, "left", "MANTÉN"), "XUTAR", "Mantén: càmera lenta per apuntar. Deixa anar: xut!"),
            Row(r => Mouse(r, "right", null), "PASSAR", "Passa cap al cursor. També de banda."),
            Row(r => Key(r, "ESPAI", KeySize.Space, 62f), "REGAT", "Una finta per deixar enrere el defensa."),
            Note("La bola la protegeixes amb el cos automàticament."));

        y = Section(c2, 0f, "SENSE LA BOLA",
            Row(r => Mouse(r, "left", null), "COP D'ESTIC", "Roba la bola o intercepta una passada."),
            Row(r => Key(r, "ESPAI", KeySize.Space, 62f), "CANVIAR", "Passa a controlar un altre jugador."));
        Section(c2, y, "PORTER",
            Row(r => Key(r, "ESPAI", KeySize.Space, 62f), "ATURA!", "Quan et xuten, prem amb la línia a la zona verda."));
    }

    void BuildTouchPage()
    {
        RectTransform c0 = Column(0), c1 = Column(1), c2 = Column(2);

        float y = Section(c0, 0f, "MOVIMENT",
            Row(r => Joystick(r), "JOYSTICK", "Arrossega el polze a l'esquerra per patinar."),
            Row(r => TouchButton(r, "SPRINT", TouchSprint, 80f, 20f), "ESPRINTAR", "Mantén-lo premut per córrer més."),
            Row(r => TouchButton(r, "II", TouchNavy, 64f, 28f), "PAUSA", "Atura el partit i obre el menú."));
        Section(c0, y, "TOT ÉS AUTOMÀTIC",
            Note("Els botons canvien de nom segons si tens la bola o no."));

        Section(c1, 0f, "AMB LA BOLA",
            Row(r => TouchButton(r, "XUTA", TouchShoot, 96f, 26f, "MANTÉN"), "XUTAR", "Mantén, arrossega per apuntar i deixa anar."),
            Row(r => TouchButton(r, "PASSA", TouchNavy, 80f, 20f), "PASSAR", "Passa la bola a un company."),
            Row(r => TouchButton(r, "REGAT", TouchNavy, 80f, 20f), "REGAT", "Una finta per deixar enrere el defensa."),
            Note("La bola la protegeixes amb el cos automàticament."));

        y = Section(c2, 0f, "SENSE LA BOLA",
            Row(r => TouchButton(r, "ROBA", TouchShoot, 96f, 26f), "COP D'ESTIC", "Roba la bola o intercepta una passada."),
            Row(r => TouchButton(r, "CANVI", TouchNavy, 80f, 20f), "CANVIAR", "Passa a controlar un altre jugador."));
        Section(c2, y, "PORTER",
            Row(r => TouchButton(r, "ATURA!", TouchQte, 90f, 20f), "ATURA!", "Quan et xuten, toca'l amb la línia a la zona verda."));
    }

    // ---------- Seccions i files ----------

    struct Entry
    {
        public Action<RectTransform> icon;   // null = nota (només text)
        public string title, text;
    }

    static Entry Row(Action<RectTransform> icon, string title, string text) =>
        new Entry { icon = icon, title = title, text = text };

    static Entry Note(string text) => new Entry { icon = null, text = text };

    RectTransform Column(int index)
    {
        float w = (CardW - BodySide * 2f - ColumnGap * 2f) / 3f;
        RectTransform col = UIKit.Rect(body, "Column" + index);
        col.anchorMin = new Vector2(0f, 0f);
        col.anchorMax = new Vector2(0f, 1f);
        col.pivot = new Vector2(0f, 1f);
        col.anchoredPosition = new Vector2(index * (w + ColumnGap), 0f);
        col.sizeDelta = new Vector2(w, 0f);
        return col;
    }

    // Targeta de paper amb capçalera fosca i les files a sota. Torna on comença la següent.
    float Section(RectTransform column, float top, string header, params Entry[] entries)
    {
        float h = HeaderH + SectionPad;
        foreach (Entry e in entries) h += e.icon != null ? RowH : NoteH;

        Image paper = UIKit.PaperPanel(column, header, 26f);
        RectTransform rt = paper.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, h);

        Image strip = UIKit.Image(rt, "Header", UIArt.Ink, 10f);
        UIKit.Stretch(strip.rectTransform, 0f);
        strip.rectTransform.anchorMin = new Vector2(0f, 1f);
        strip.rectTransform.offsetMin = new Vector2(12f, -HeaderH);
        strip.rectTransform.offsetMax = new Vector2(-12f, -10f);

        TextMeshProUGUI ht = UIKit.Text(strip.rectTransform, "Label", header, 30f, Color.white);
        GameFonts.Style(ht, GameFonts.Role.Button);
        UIKit.Stretch(ht.rectTransform, 14f, 2f, 14f, 2f);

        float y = HeaderH + 4f;
        for (int i = 0; i < entries.Length; i++)
        {
            Entry e = entries[i];
            float rowH = e.icon != null ? RowH : NoteH;

            RectTransform row = UIKit.Rect(rt, "Row" + i);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(-24f, rowH);

            if (i > 0)
            {
                Image line = UIKit.Image(row, "Line", UIKit.Alpha(UIArt.Ink, 0.15f));
                line.rectTransform.anchorMin = new Vector2(0f, 1f);
                line.rectTransform.anchorMax = new Vector2(1f, 1f);
                line.rectTransform.pivot = new Vector2(0.5f, 1f);
                line.rectTransform.anchoredPosition = Vector2.zero;
                line.rectTransform.sizeDelta = new Vector2(-20f, 2f);
            }

            if (e.icon != null) BuildRow(row, e);
            else BuildNote(row, e.text);

            y += rowH;
        }
        return top + h + SectionGap;
    }

    void BuildRow(RectTransform row, Entry e)
    {
        RectTransform icon = UIKit.Rect(row, "Icon");
        icon.anchorMin = new Vector2(0f, 0f);
        icon.anchorMax = new Vector2(0f, 1f);
        icon.pivot = new Vector2(0f, 0.5f);
        icon.anchoredPosition = new Vector2(6f, 0f);
        icon.sizeDelta = new Vector2(IconW, 0f);
        e.icon(icon);

        float left = IconW + 18f;

        TextMeshProUGUI t = UIKit.Text(row, "Title", e.title, 28f, UIArt.ArtRed, FontStyles.Bold,
                                       TextAlignmentOptions.BottomLeft);
        GameFonts.Style(t, GameFonts.Role.Title);
        if (t.font != null) t.fontSharedMaterial = t.font.material;   // sense ombra: va sobre paper
        t.rectTransform.anchorMin = new Vector2(0f, 1f);
        t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.pivot = new Vector2(0f, 1f);
        t.rectTransform.offsetMin = new Vector2(left, -46f);
        t.rectTransform.offsetMax = new Vector2(-6f, -10f);

        TextMeshProUGUI d = UIKit.Text(row, "Text", e.text, 20f, UIArt.Ink, FontStyles.Normal,
                                       TextAlignmentOptions.TopLeft);
        d.textWrappingMode = TextWrappingModes.Normal;
        d.overflowMode = TextOverflowModes.Overflow;
        d.enableAutoSizing = true;
        d.fontSizeMax = 20f;
        d.fontSizeMin = 15f;
        d.lineSpacing = -8f;
        d.rectTransform.anchorMin = new Vector2(0f, 0f);
        d.rectTransform.anchorMax = new Vector2(1f, 1f);
        d.rectTransform.offsetMin = new Vector2(left, 6f);
        d.rectTransform.offsetMax = new Vector2(-6f, -50f);
    }

    void BuildNote(RectTransform row, string text)
    {
        Image star = UIKit.CircleImage(row, "Dot", UIKit.Accent, 22f);
        UIKit.Place(star.rectTransform, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(22f, 22f));

        TextMeshProUGUI d = UIKit.Text(row, "Text", text, 20f, UIArt.InkBrown, FontStyles.Italic,
                                       TextAlignmentOptions.Left);
        d.textWrappingMode = TextWrappingModes.Normal;
        d.enableAutoSizing = true;
        d.fontSizeMax = 20f;
        d.fontSizeMin = 15f;
        UIKit.Stretch(d.rectTransform, 56f, 4f, 8f, 4f);
    }

    // ---------- Dibuixos de tecles i botons ----------

    enum KeySize { Normal, Wide, Space }

    static Sprite Art(string name) => Resources.Load<Sprite>(ArtFolder + name);

    // Proporcions dels dibuixos (amplada / alçada): key_1u 150×170, key_wide 316×170, key_space 482×170
    static float Aspect(KeySize k) => k == KeySize.Wide ? 316f / 170f : k == KeySize.Space ? 482f / 170f : 150f / 170f;

    static RectTransform Key(RectTransform parent, string label, KeySize size, float height, Vector2 offset = default)
    {
        string file = size == KeySize.Wide ? "key_wide" : size == KeySize.Space ? "key_space" : "key_1u";
        Sprite sprite = Art(file);

        Image img = UIKit.Image(parent, "Key " + label, Color.white);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = UIArt.Steel;   // sense el dibuix: tecla senzilla
            UIKit.SetRounded(img, 10f);
        }
        RectTransform rt = img.rectTransform;
        UIKit.Place(rt, new Vector2(0.5f, 0.5f), offset, new Vector2(height * Aspect(size), height));
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;

        // L'etiqueta va a la cara de dalt de la tecla (la part de baix és el lateral)
        float fontSize = label.Length <= 1 ? height * 0.46f : height * 0.3f;
        TextMeshProUGUI t = UIKit.Text(rt, "Label", label, fontSize, Color.white, FontStyles.Bold);
        GameFonts.Style(t, GameFonts.Role.Button);
        UIKit.Stretch(t.rectTransform, height * 0.12f, height * 0.2f, height * 0.12f, height * 0.06f);
        return rt;
    }

    static void KeyWASD(RectTransform parent)
    {
        const float h = 46f;
        float w = h * Aspect(KeySize.Normal), gap = 4f;
        Key(parent, "W", KeySize.Normal, h, new Vector2(0f, h * 0.5f + 1f));
        Key(parent, "A", KeySize.Normal, h, new Vector2(-(w + gap), -h * 0.5f + 1f));
        Key(parent, "S", KeySize.Normal, h, new Vector2(0f, -h * 0.5f + 1f));
        Key(parent, "D", KeySize.Normal, h, new Vector2(w + gap, -h * 0.5f + 1f));
    }

    static void KeyPair(RectTransform parent, string a, string b)
    {
        const float h = 70f;
        float w = h * Aspect(KeySize.Normal);
        Key(parent, a, KeySize.Normal, h, new Vector2(-(w * 0.5f + 6f), 0f));
        Key(parent, b, KeySize.Normal, h, new Vector2(w * 0.5f + 6f, 0f));
    }

    // button: null, "left" o "right". hold: etiqueta que batega al costat (p. ex. "MANTÉN")
    void Mouse(RectTransform parent, string button, string hold)
    {
        string file = button == "left" ? "mouse_left" : button == "right" ? "mouse_right" : "mouse";
        Sprite sprite = Art(file);
        const float h = 88f;
        float w = h * 230f / 330f;
        float x = hold != null ? -46f : 0f;

        Image img = UIKit.Image(parent, "Mouse", Color.white);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = UIArt.Paper;
            UIKit.SetRounded(img, 30f);
        }
        UIKit.Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = new Vector2(x, 0f);

        if (hold != null) HoldTag(parent, hold, new Vector2(x + w * 0.5f + 52f, 0f));
    }

    void HoldTag(RectTransform parent, string label, Vector2 pos)
    {
        Image pill = UIKit.Image(parent, "Hold", UIKit.Accent, 14f);
        UIKit.Place(pill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 30f));
        pill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        pill.rectTransform.anchoredPosition = pos;

        TextMeshProUGUI t = UIKit.Text(pill.rectTransform, "Label", label, 17f, Color.white, FontStyles.Bold);
        GameFonts.Style(t, GameFonts.Role.Button);
        UIKit.Stretch(t.rectTransform, 6f, 1f, 6f, 1f);
        pulsing.Add(pill);
    }

    // Mateix aspecte que els botons de TouchControls: cercle de color, vora clara i el nom a dins
    void TouchButton(RectTransform parent, string label, Color color, float size, float fontSize, string hold = null)
    {
        float x = hold != null ? -40f : 0f;

        Image shadow = UIKit.CircleImage(parent, label + " Shadow", UIKit.Alpha(UIArt.Ink, 0.35f), size);
        shadow.rectTransform.anchoredPosition = new Vector2(x + 3f, -4f);

        Image face = UIKit.CircleImage(parent, label, color, size);
        face.rectTransform.anchoredPosition = new Vector2(x, 0f);

        Image rim = UIKit.CircleImage(face.transform, "Rim", new Color(1f, 1f, 1f, 0.22f), size);
        Image inner = UIKit.CircleImage(rim.transform, "Inner", color, size - 8f);
        rim.rectTransform.anchoredPosition = inner.rectTransform.anchoredPosition = Vector2.zero;

        TextMeshProUGUI t = UIKit.Text(face.transform, "Label", label, fontSize, Color.white, FontStyles.Bold);
        GameFonts.Style(t, GameFonts.Role.Button);
        UIKit.Stretch(t.rectTransform, 6f);

        if (hold != null) HoldTag(parent, hold, new Vector2(x + size * 0.5f + 50f, 0f));
    }

    static void Joystick(RectTransform parent)
    {
        Image baseImg = UIKit.CircleImage(parent, "JoyBase", UIKit.Alpha(UIArt.Ink, 0.18f), 92f);
        Image ring = UIKit.CircleImage(baseImg.transform, "Ring", UIKit.Alpha(UIArt.Ink, 0.22f), 64f);
        ring.rectTransform.anchoredPosition = Vector2.zero;
        Image knob = UIKit.CircleImage(baseImg.transform, "Knob", UIArt.Steel, 44f);
        knob.rectTransform.anchoredPosition = new Vector2(14f, 12f);
    }

    // ---------- Ajuda per als menús ----------

    // Fa una còpia d'un botó que ja existeix (mateix estil i mida) amb un altre text, just abans o
    // després d'ell. Així el menú d'inici i el de pausa tenen CONTROLS sense haver de refer-los.
    public static Button CloneButton(Button template, string name, string label, bool before)
    {
        if (template == null) return null;

        GameObject go = Instantiate(template.gameObject, template.transform.parent);
        go.name = name;
        int index = template.transform.GetSiblingIndex();
        go.transform.SetSiblingIndex(before ? index : index + 1);

        Button b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent();   // fora els listeners copiats de la plantilla
        b.interactable = true;

        TMP_Text[] texts = go.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text main = null;
        foreach (TMP_Text t in texts)
        {
            if (!t.gameObject.activeSelf || string.IsNullOrEmpty(t.text)) continue;
            if (main == null || t.fontSize > main.fontSize) main = t;   // el text principal és el més gran
        }
        if (main != null) main.text = label;
        return b;
    }
}
