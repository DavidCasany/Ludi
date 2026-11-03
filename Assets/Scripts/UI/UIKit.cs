using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Piezas para montar UI por código con el estilo de los menús (fondo azul noche, acento naranja).
// Las pantallas del torneo se construyen en tiempo de ejecución porque dependen de los datos
// (número de equipos, rondas, mejoras...). Los sprites redondeados se generan aquí: no hace falta
// ningún asset ni recurso del editor, así que funciona igual en la build.
public static class UIKit
{
    // ---------- Paleta ----------

    public static readonly Color Background = new Color(0.07f, 0.09f, 0.13f);
    public static readonly Color Panel = new Color(0.11f, 0.15f, 0.2f);
    public static readonly Color Surface = new Color(0.16f, 0.21f, 0.28f);
    public static readonly Color SurfaceHover = new Color(0.24f, 0.31f, 0.41f);
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.12f);
    public static readonly Color TextMain = new Color(0.93f, 0.95f, 0.98f);
    public static readonly Color TextSoft = new Color(0.62f, 0.68f, 0.77f);
    public static readonly Color TextFaint = new Color(0.42f, 0.47f, 0.55f);
    public static readonly Color Accent = new Color(0.95f, 0.47f, 0.17f);
    public static readonly Color AccentHover = new Color(1f, 0.58f, 0.3f);
    public static readonly Color AccentPressed = new Color(0.75f, 0.35f, 0.1f);
    public static readonly Color Player = new Color(0.3f, 0.6f, 0.95f);
    public static readonly Color Good = new Color(0.42f, 0.85f, 0.52f);
    public static readonly Color Bad = new Color(0.95f, 0.42f, 0.42f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

    // ---------- Sprites generados ----------

    const int RoundedSize = 64;
    const int RoundedRadius = 16;
    static Sprite rounded, circle;

    // Rectángulo con esquinas redondeadas, 9-slice. Con Image.pixelsPerUnitMultiplier se cambia el radio.
    public static Sprite Rounded
    {
        get
        {
            if (rounded == null) rounded = MakeRounded();
            return rounded;
        }
    }

    public static Sprite Circle
    {
        get
        {
            if (circle == null) circle = MakeCircle(128);
            return circle;
        }
    }

    static Sprite MakeRounded()
    {
        int s = RoundedSize;
        float r = RoundedRadius;
        Texture2D tex = NewTexture(s);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // Distancia al rectángulo interior (las esquinas son cuartos de círculo de radio r)
                float cx = Mathf.Clamp(x + 0.5f, r, s - r);
                float cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    static Sprite MakeCircle(int s)
    {
        Texture2D tex = NewTexture(s);
        float c = s * 0.5f, r = c - 1f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewTexture(int s)
    {
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.hideFlags = HideFlags.DontSave;
        return tex;
    }

    // ---------- Canvas ----------

    // Canvas de pantalla completa a 1920×1080 (se adapta a cualquier resolución)
    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                                       typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }

    // ---------- Elementos ----------

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // radius > 0 = esquinas redondeadas de ese radio (en píxeles de referencia)
    public static Image Image(Transform parent, string name, Color color, float radius = 0f, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        if (radius > 0f) SetRounded(img, radius);
        return img;
    }

    public static void SetRounded(Image img, float radius)
    {
        img.sprite = Rounded;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = RoundedRadius / Mathf.Max(1f, radius);
    }

    public static Image CircleImage(Transform parent, string name, Color color, float size)
    {
        Image img = Image(parent, name, color);
        img.sprite = Circle;
        img.preserveAspect = true;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
                                       FontStyles style = FontStyles.Normal,
                                       TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        return t;
    }

    // Botón redondeado con texto. 'primary' = naranja (acción principal), si no, gris azulado.
    public static Button Button(Transform parent, string name, string label, bool primary, float fontSize = 32f)
    {
        Image img = Image(parent, name, Color.white, 14f, true);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.colors = primary ? Colors(Accent, AccentHover, AccentHover)
                           : Colors(Surface, Accent, Accent);

        TextMeshProUGUI t = Text(img.transform, "Label", label, fontSize, TextMain, FontStyles.Bold);
        t.characterSpacing = 4f;
        Stretch(t.rectTransform, 8f);
        return b;
    }

    public static ColorBlock Colors(Color normal, Color highlighted, Color selected)
    {
        ColorBlock c = ColorBlock.defaultColorBlock;
        c.normalColor = normal;
        c.highlightedColor = highlighted;
        c.selectedColor = selected;
        c.pressedColor = AccentPressed;
        c.disabledColor = new Color(0.13f, 0.14f, 0.16f, 0.9f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        return c;
    }

    // Escudo del equipo: su crest si lo tiene; si no, un círculo de su color con el nombre corto
    public static RectTransform TeamBadge(Transform parent, TeamData team, float size)
    {
        Image ring = CircleImage(parent, "Badge", team != null ? team.PrimaryColor : Surface, size);
        if (team == null) return ring.rectTransform;

        if (team.Crest != null)
        {
            ring.color = new Color(1f, 1f, 1f, 0.06f);
            Image crest = Image(ring.transform, "Crest", Color.white);
            crest.sprite = team.Crest;
            crest.preserveAspect = true;
            Stretch(crest.rectTransform, size * 0.08f);
        }
        else
        {
            Color c = team.PrimaryColor;
            float luma = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;   // texto oscuro sobre colores claros
            TextMeshProUGUI t = Text(ring.transform, "Initials", team.ShortName, size * 0.32f,
                                     luma > 0.6f ? Background : Color.white, FontStyles.Bold);
            Stretch(t.rectTransform, 0f);
        }
        return ring.rectTransform;
    }

    // ---------- Layout ----------

    // Ancla y pivote en el mismo punto: 'pos' es la distancia a ese borde/esquina
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt, float padding) => Stretch(rt, padding, padding, padding, padding);

    public static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // Esquina superior izquierda de 'rt' en (x, -y) dentro del padre, con tamaño fijo (para el cuadro)
    public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static LayoutElement Size(Component c, float w, float h, float flexibleWidth = 0f)
    {
        LayoutElement le = c.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        if (w >= 0f) { le.minWidth = w; le.preferredWidth = w; }
        if (h >= 0f) { le.minHeight = h; le.preferredHeight = h; }
        le.flexibleWidth = flexibleWidth;
        return le;
    }

    public static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    public static void Select(Selectable s)
    {
        if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // ---------- Input (mismo patrón que el resto de menús) ----------

    public static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        var g = UnityEngine.InputSystem.Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);
#endif
    }

    // Tecla numérica 1..9 pulsada este frame (fila de arriba o teclado numérico); 0 = ninguna
    public static int NumberPressed()
    {
        for (int i = 1; i <= 9; i++)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return 0;
            if (k[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit1 + i - 1)].wasPressedThisFrame ||
                k[(UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Numpad1 + i - 1)].wasPressedThisFrame)
                return i;
#else
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
#endif
        }
        return 0;
    }
}
