#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

// Construye la pantalla de ajustes en la escena abierta: Tools > Ludi > Crear menú de ajustes
//  - Crea Assets/Resources/SettingsData.asset si no existe (valores por defecto y resoluciones)
//  - Monta la UI con todas las referencias y la navegación de teclado/mando conectadas
//
// Distribució (mateix estil que la pantalla de CONTROLS):
//   targeta metàl·lica centrada amb el títol CONFIGURACIÓ a dalt a l'esquerra
//   tres columnes de targetes de paper amb capçalera fosca:
//     ÀUDIO (música, efectes) + JOC (fletxes) · PANTALLA (resolució, pantalla completa) · CONTROLS (tàctils)
//   a baix: botó TORNAR + "Esc · Tornar"
// Hi cap tot sense scroll també en 4:3 (tauletes) i en mòbils molt allargats.
// En la web el desplegable de resolución pasa a ser "Resolució de renderitzat" (lo hace SettingsMenuUI).
// Todo son GameObjects normales: después se puede retocar a mano.
public static class SettingsMenuBuilder
{
    const string RootName = "SettingsUI";
    const string DataFolder = "Assets/Resources";
    const string DataPath = DataFolder + "/SettingsData.asset";

    // Medidas a 1920×1080 (las más útiles para retocar están luego en la jerarquía, ver instrucciones)
    // Targeta i columnes com la pantalla de CONTROLS (1680 × 940, tres columnes de paper)
    const float CardWidth = 1680f, CardHeight = 940f;
    const float BodyTop = 190f, BodySide = 50f;
    const float ColumnGap = 30f;
    const float ColumnWidth = (CardWidth - BodySide * 2f - ColumnGap * 2f) / 3f;
    const float ColumnSpacing = 14f;   // entre elements d'una targeta
    const float SectionGap = 24f;      // entre targetes d'una columna
    const float HeaderHeight = 48f;
    const float LabelHeight = 36f;
    const float SliderHeight = 30f;
    const float DropdownHeight = 60f;
    const float ToggleRowHeight = 56f;
    const float ToggleBoxSize = 44f;
    const float LabelSize = 26f;

    static readonly Color BackgroundColor = new Color(0.07f, 0.09f, 0.13f, 0.97f);
    static readonly Color Surface = new Color(0.16f, 0.21f, 0.28f);           // pistas, cajas, desplegable
    static readonly Color SurfaceHover = new Color(0.24f, 0.31f, 0.41f);
    static readonly Color ListColor = new Color(0.11f, 0.15f, 0.2f);          // lista abierta del desplegable
    static readonly Color TextMain = new Color(0.93f, 0.95f, 0.98f);
    static readonly Color TextSoft = new Color(0.62f, 0.68f, 0.77f);
    static readonly Color Accent = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color AccentPressed = new Color(0.75f, 0.35f, 0.1f);
    static readonly Color PanelColor = new Color(0.11f, 0.15f, 0.2f);
    static readonly Color OnPaper = UIArt.Ink;          // text sobre les targetes de paper
    static readonly Color ValueColor = UIArt.ArtRed;    // percentatges

    static Sprite uiSprite, knobSprite, checkSprite;

    [MenuItem("Tools/Ludi/Crear menú de ajustes")]
    static void Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null &&
            !EditorUtility.DisplayDialog("Menú de ajustes",
                "Ya existe '" + RootName + "' en la escena. Se borrará y se creará de nuevo: " +
                "los cambios hechos a mano dentro de él se perderán.", "Sustituir", "Cancelar"))
            return;

        EnsureSettingsData();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        if (old != null) Undo.DestroyObjectImmediate(old);

        // Recursos del UI por defecto de Unity (los mismos que usa GameObject > UI)
        uiSprite = Builtin("UI/Skin/UISprite.psd");
        knobSprite = Builtin("UI/Skin/Knob.psd");
        checkSprite = Builtin("UI/Skin/Checkmark.psd");
        TMP_DefaultControls.Resources tmpRes = new TMP_DefaultControls.Resources
        {
            standard = uiSprite,
            background = Builtin("UI/Skin/Background.psd"),
            inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
            knob = knobSprite,
            checkmark = checkSprite,
            dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
            mask = Builtin("UI/Skin/UIMask.psd")
        };
        DefaultControls.Resources uiRes = new DefaultControls.Resources
        {
            standard = tmpRes.standard,
            background = tmpRes.background,
            inputField = tmpRes.inputField,
            knob = tmpRes.knob,
            checkmark = tmpRes.checkmark,
            dropdown = tmpRes.dropdown,
            mask = tmpRes.mask
        };

        // ---------- Canvas (por encima del menú de pausa, que usa 100) ----------
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                         typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Expand: el lienzo mide al menos 1920×1080 en cualquier formato (móvil 20:9, tableta 4:3...)
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Image bg = CreateImage(root.transform, "Background", BackgroundColor, null);
        Stretch(bg.rectTransform, 0f);   // tapa el partido cuando se abre desde la pausa y bloquea los clics

        // ---------- Targeta metàl·lica (mateix estil que la pantalla de CONTROLS) ----------
        Image card = CreateImage(root.transform, "Card", Color.white, UIArt.SteelPanel);
        card.raycastTarget = true;
        if (UIArt.SteelPanel != null)
        {
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = UIArt.PanelSpriteBorder / 40f;
        }
        else
        {
            card.sprite = uiSprite;
            card.type = Image.Type.Sliced;
            card.color = PanelColor;
        }
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
        Transform c = card.transform;

        // Capçalera a dalt a l'esquerra: barra d'accent, títol i subtítol
        Image bar = CreateImage(c, "AccentBar", Accent, null);
        Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -44f), new Vector2(110f, 8f));

        TextMeshProUGUI title = CreateText(c, "Title", "CONFIGURACIÓ", 72f, FontStyles.Bold, TextMain,
                                           TextAlignmentOptions.Left);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        GameFonts.Style(title, GameFonts.Role.Title);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(66f, -58f), new Vector2(900f, 84f));

        TextMeshProUGUI subtitle = CreateText(c, "Subtitle", "So, pantalla i controls", 26f, FontStyles.Normal, TextSoft,
                                              TextAlignmentOptions.Left);
        GameFonts.Style(subtitle, GameFonts.Role.Text);
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(70f, -140f), new Vector2(900f, 36f));

        // ---------- Opcions: tres columnes de targetes de paper ----------
        Transform col0 = CreateColumn(c, "Column0", 0);
        Transform col1 = CreateColumn(c, "Column1", 1);
        Transform col2 = CreateColumn(c, "Column2", 2);

        Transform audio = CreateSection(col0, "ÀUDIO");
        TextMeshProUGUI musicValue, sfxValue;
        Slider music = CreateSliderBlock(audio, "Musica", "Música", uiRes, out musicValue);
        Slider sfx = CreateSliderBlock(audio, "Efectes", "Efectes de so", uiRes, out sfxValue);

        Transform game = CreateSection(col0, "JOC");
        Toggle arrows = CreateToggleRow(game, "FlechasPase", "Fletxes de passada");

        Transform screen = CreateSection(col1, "PANTALLA");
        TMP_Dropdown dropdown = CreateDropdownBlock(screen, "Resolucion", "Resolució", tmpRes);
        Toggle fullscreen = CreateToggleRow(screen, "PantallaCompleta", "Pantalla completa");

        // Tercera columna: controls (sobretot per a mòbil i tauleta)
        Transform controls = CreateSection(col2, "CONTROLS");
        TMP_Dropdown controlMode = CreateDropdownBlock(controls, "ModeControl", "Mode de control", tmpRes);
        TextMeshProUGUI touchScaleValue, touchOpacityValue;
        Slider touchScale = CreateSliderBlock(controls, "MidaControls", "Mida dels botons tàctils", uiRes, out touchScaleValue);
        touchScale.minValue = GameSettings.TouchScaleMin;
        touchScale.maxValue = GameSettings.TouchScaleMax;
        touchScale.value = 1f;
        touchScaleValue.text = "100 %";
        Slider touchOpacity = CreateSliderBlock(controls, "OpacitatControls", "Opacitat dels botons", uiRes,
                                                out touchOpacityValue);
        touchOpacity.minValue = GameSettings.TouchOpacityMin;
        touchOpacity.maxValue = GameSettings.TouchOpacityMax;
        touchOpacity.value = 0.7f;
        touchOpacityValue.text = "70 %";
        Toggle leftHanded = CreateToggleRow(controls, "Esquerrans", "Joystick a la dreta");
        leftHanded.isOn = false;

        // ---------- Tornar (a baix, centrat dins la targeta) ----------
        Button back = CreateButton(c, "Volver", "TORNAR");
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(340f, 84f));

        TextMeshProUGUI hint = CreateText(c, "Hint", "Esc · Tornar", 22f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Right);
        GameFonts.Style(hint, GameFonts.Role.Text);
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-70f, 58f), new Vector2(300f, 36f));

        // ---------- Navegación con teclado/mando: de arriba abajo, en el orden visual ----------
        // (izquierda/derecha en los sliders cambian el volumen; eso lo hace el propio Slider)
        Selectable[] order = { music, sfx, arrows, dropdown, fullscreen,
                               controlMode, touchScale, touchOpacity, leftHanded, back };
        for (int i = 0; i < order.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = order[(i - 1 + order.Length) % order.Length];
            nav.selectOnDown = order[(i + 1) % order.Length];
            order[i].navigation = nav;
        }

        // ---------- Script ----------
        SettingsMenuUI ui = root.AddComponent<SettingsMenuUI>();
        Wire(ui, "resolutionDropdown", dropdown);
        Wire(ui, "fullscreenToggle", fullscreen);
        Wire(ui, "musicSlider", music);
        Wire(ui, "musicValue", musicValue);
        Wire(ui, "sfxSlider", sfx);
        Wire(ui, "sfxValue", sfxValue);
        Wire(ui, "passArrowsToggle", arrows);
        Wire(ui, "controlModeDropdown", controlMode);
        Wire(ui, "touchScaleSlider", touchScale);
        Wire(ui, "touchScaleValue", touchScaleValue);
        Wire(ui, "touchOpacitySlider", touchOpacity);
        Wire(ui, "touchOpacityValue", touchOpacityValue);
        Wire(ui, "leftHandedToggle", leftHanded);
        Wire(ui, "backButton", back);

        Undo.RegisterCreatedObjectUndo(root, "Crear menú de ajustes");
        EnsureEventSystem();
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[SettingsMenuBuilder] Menú de ajustes creado en la escena '" + root.scene.name + "'.");
    }

    // ---------- Bloques de la columna ----------

    // Columna i (0, 1, 2) dins de la targeta: les targetes de paper s'apilen de dalt a baix
    static Transform CreateColumn(Transform parent, string name, int index)
    {
        GameObject col = new GameObject(name, typeof(RectTransform));
        col.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)col.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(BodySide + index * (ColumnWidth + ColumnGap), -BodyTop);
        rt.sizeDelta = new Vector2(ColumnWidth, 0f);

        VerticalLayoutGroup v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = SectionGap;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return col.transform;
    }

    // Targeta de paper amb capçalera fosca (franja tinta i títol blanc). Torna on van els controls.
    static Transform CreateSection(Transform parent, string text)
    {
        Image paper = CreateImage(parent, "Seccion_" + text, Color.white, UIArt.PaperPanel);
        paper.raycastTarget = false;
        if (UIArt.PaperPanel != null)
        {
            paper.type = Image.Type.Sliced;
            paper.pixelsPerUnitMultiplier = UIArt.PanelSpriteBorder / 26f;
        }
        else
        {
            paper.sprite = uiSprite;
            paper.type = Image.Type.Sliced;
            paper.color = UIArt.Paper;
        }
        VerticalLayoutGroup v = paper.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(12, 12, 10, 20);
        v.spacing = 10f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        Image strip = CreateImage(paper.transform, "Header", UIArt.Ink, uiSprite);
        strip.type = Image.Type.Sliced;
        strip.raycastTarget = false;
        SetSize(strip.gameObject, -1f, HeaderHeight, 1f);

        TextMeshProUGUI t = CreateText(strip.transform, "Text", text, 30f, FontStyles.Bold, Color.white,
                                       TextAlignmentOptions.Center);
        GameFonts.Style(t, GameFonts.Role.Button);
        Stretch(t.rectTransform, 4f);

        // Contingut amb una mica més de marge que la capçalera
        GameObject content = NewBlock(paper.transform, "Content", ColumnSpacing, 4);
        content.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(14, 14, 4, 0);
        return content.transform;
    }

    // Etiqueta y porcentaje en una línea, la pista del slider debajo
    static Slider CreateSliderBlock(Transform parent, string name, string label, DefaultControls.Resources res,
                                    out TextMeshProUGUI valueLabel)
    {
        GameObject block = NewBlock(parent, name, 6f, 0);

        GameObject line = NewRow(block.transform, "Header", LabelHeight, 10f);
        TextMeshProUGUI l = CreateText(line.transform, "Label", label, LabelSize, FontStyles.Normal, OnPaper,
                                       TextAlignmentOptions.MidlineLeft);
        GameFonts.Style(l, GameFonts.Role.Text);
        SetSize(l.gameObject, -1f, LabelHeight, 1f);
        valueLabel = CreateText(line.transform, "Value", "80 %", LabelSize, FontStyles.Bold, ValueColor,
                                TextAlignmentOptions.MidlineRight);
        GameFonts.Style(valueLabel, GameFonts.Role.Text);
        SetSize(valueLabel.gameObject, 100f, LabelHeight, 0f);

        GameObject go = DefaultControls.CreateSlider(res);
        go.name = "Slider";
        go.transform.SetParent(block.transform, false);
        SetSize(go, -1f, SliderHeight, 1f);

        Slider s = go.GetComponent<Slider>();
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = 0.8f;

        // Pista oscura, relleno de acento, tirador blanco redondo que se ilumina al seleccionarlo
        Image track = go.transform.Find("Background").GetComponent<Image>();
        track.color = Surface;
        if (s.fillRect != null) s.fillRect.GetComponent<Image>().color = Accent;
        if (s.handleRect != null)
        {
            s.handleRect.sizeDelta = new Vector2(SliderHeight, 0f);
            s.handleRect.GetComponent<Image>().sprite = knobSprite;
        }
        ColorBlock c = s.colors;
        c.normalColor = TextMain;
        c.highlightedColor = Color.white;
        c.selectedColor = new Color(1f, 0.82f, 0.68f);
        c.pressedColor = new Color(1f, 0.82f, 0.68f);
        s.colors = c;
        return s;
    }

    // Etiqueta encima y desplegable debajo, a todo el ancho de la columna
    static TMP_Dropdown CreateDropdownBlock(Transform parent, string name, string label, TMP_DefaultControls.Resources res)
    {
        GameObject block = NewBlock(parent, name, 8f, 0);

        TextMeshProUGUI l = CreateText(block.transform, "Label", label, LabelSize, FontStyles.Normal, OnPaper,
                                       TextAlignmentOptions.MidlineLeft);
        GameFonts.Style(l, GameFonts.Role.Text);
        SetSize(l.gameObject, -1f, LabelHeight, 1f);

        GameObject go = TMP_DefaultControls.CreateDropdown(res);
        go.name = "Dropdown";
        go.transform.SetParent(block.transform, false);
        SetSize(go, -1f, DropdownHeight, 1f);

        TMP_Dropdown dd = go.GetComponent<TMP_Dropdown>();
        dd.image.color = Color.white;               // el color real lo pone el ColorBlock
        dd.colors = ButtonColors(Surface, SurfaceHover, Accent);
        dd.captionText.fontSize = 26f;
        GameFonts.Style(dd.captionText, GameFonts.Role.Text);
        GameFonts.Style(dd.itemText, GameFonts.Role.Text);
        dd.captionText.color = TextMain;
        dd.captionText.rectTransform.offsetMin = new Vector2(24f, dd.captionText.rectTransform.offsetMin.y);
        dd.itemText.fontSize = 28f;
        dd.itemText.color = TextMain;

        RectTransform arrow = (RectTransform)go.transform.Find("Arrow");
        if (arrow != null)
        {
            arrow.GetComponent<Image>().color = TextMain;
            arrow.sizeDelta = new Vector2(28f, 28f);
            arrow.anchoredPosition = new Vector2(-28f, 0f);
        }

        // Lista desplegable oscura, alta y con elementos grandes (por defecto son de 20 px)
        RectTransform template = dd.template;
        template.sizeDelta = new Vector2(template.sizeDelta.x, 300f);
        template.GetComponent<Image>().color = ListColor;

        RectTransform content = (RectTransform)template.Find("Viewport/Content");
        content.sizeDelta = new Vector2(content.sizeDelta.x, 56f);
        RectTransform item = (RectTransform)content.Find("Item");
        item.sizeDelta = new Vector2(item.sizeDelta.x, 56f);

        Toggle itemToggle = item.GetComponent<Toggle>();
        itemToggle.colors = ButtonColors(ListColor, SurfaceHover, Accent);
        Transform check = item.Find("Item Checkmark");
        if (check != null) check.GetComponent<Image>().color = Accent;

        Transform scrollbar = template.Find("Scrollbar");
        if (scrollbar != null)
        {
            scrollbar.GetComponent<Image>().color = ListColor;
            Scrollbar sb = scrollbar.GetComponent<Scrollbar>();
            if (sb != null && sb.handleRect != null) sb.handleRect.GetComponent<Image>().color = Surface;
        }
        return dd;
    }

    // Fila entera clicable: caja con marca + texto (como en el blocking)
    static Toggle CreateToggleRow(Transform parent, string name, string label)
    {
        GameObject row = NewRow(parent, name, ToggleRowHeight, 22f);
        // Image transparente para que se pueda hacer clic en toda la fila, también sobre el texto
        Image hit = row.AddComponent<Image>();
        hit.color = Color.clear;

        Image box = CreateImage(row.transform, "Box", Color.white, uiSprite);
        box.type = Image.Type.Sliced;
        SetSize(box.gameObject, ToggleBoxSize, ToggleBoxSize, 0f);

        Image check = CreateImage(box.transform, "Check", Color.white, checkSprite);
        check.preserveAspect = true;
        check.raycastTarget = false;
        Stretch(check.rectTransform, 9f);

        TextMeshProUGUI t = CreateText(row.transform, "Label", label, LabelSize, FontStyles.Normal, OnPaper,
                                       TextAlignmentOptions.MidlineLeft);
        GameFonts.Style(t, GameFonts.Role.Text);
        SetSize(t.gameObject, -1f, ToggleRowHeight, 1f);

        Toggle toggle = row.AddComponent<Toggle>();
        toggle.targetGraphic = box;     // la caja cambia de color al pasar el ratón o seleccionarla
        toggle.graphic = check;         // la marca aparece/desaparece
        toggle.colors = ButtonColors(Surface, SurfaceHover, Accent);
        toggle.isOn = true;
        return toggle;
    }

    static Button CreateButton(Transform parent, string name, string label)
    {
        Image img = CreateImage(parent, name, Color.white, uiSprite);
        img.type = Image.Type.Sliced;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.colors = ButtonColors(Surface, Accent, Accent);

        TextMeshProUGUI t = CreateText(img.transform, "Text", label, 36f, FontStyles.Bold, TextMain,
                                       TextAlignmentOptions.Center);
        t.characterSpacing = 4f;
        GameFonts.Style(t, GameFonts.Role.Button);
        Stretch(t.rectTransform, 0f);
        return b;
    }

    // ---------- Ayudantes de layout ----------

    // Bloque vertical que se adapta a su contenido (lo coloca la columna)
    static GameObject NewBlock(Transform parent, string name, float spacing, int paddingTop)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        VerticalLayoutGroup v = go.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = new RectOffset(0, 0, paddingTop, 0);
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return go;
    }

    static GameObject NewRow(Transform parent, string name, float height, float spacing)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        SetSize(go, -1f, height, 1f);
        return go;
    }

    static ColorBlock ButtonColors(Color normal, Color highlighted, Color selected)
    {
        ColorBlock c = ColorBlock.defaultColorBlock;
        c.normalColor = normal;
        c.highlightedColor = highlighted;
        c.selectedColor = selected;
        c.pressedColor = AccentPressed;
        c.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        return c;
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        return img;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style,
                                      Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        return t;
    }

    // width < 0 = sin ancho preferido; flexibleWidth 1 = ocupa el espacio que sobre
    static void SetSize(GameObject go, float width, float height, float flexibleWidth)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width >= 0f) { le.minWidth = width; le.preferredWidth = width; }
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleWidth = flexibleWidth;
    }

    // Ancla y pivote en el mismo punto: 'pos' es la distancia a ese borde
    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[SettingsMenuBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
    }

    static void EnsureSettingsData()
    {
        if (AssetDatabase.LoadAssetAtPath<SettingsData>(DataPath) != null) return;

        if (!AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
        SettingsData data = ScriptableObject.CreateInstance<SettingsData>();
        AssetDatabase.CreateAsset(data, DataPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[SettingsMenuBuilder] Creado " + DataPath + " con los valores por defecto.");
    }
}
#endif