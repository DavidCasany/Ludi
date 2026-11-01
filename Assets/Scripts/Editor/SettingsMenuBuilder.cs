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
// Distribución (sigue el blocking, sin panel que lo encierre):
//   arriba ........ título AJUSTES + barra de acento
//   centro ........ columna: AUDIO (música, efectos) · PANTALLA (resolución, pantalla completa) · JUEGO (flechas)
//   abajo ......... botón VOLVER + "Esc · Volver"
// Todo son GameObjects normales: después se puede retocar a mano.
public static class SettingsMenuBuilder
{
    const string RootName = "SettingsUI";
    const string DataFolder = "Assets/Resources";
    const string DataPath = DataFolder + "/SettingsData.asset";

    // Medidas a 1920×1080 (las más útiles para retocar están luego en la jerarquía, ver instrucciones)
    const float ColumnWidth = 760f;
    const float ColumnSpacing = 14f;
    const float SectionGap = 22f;     // aire extra encima de cada sección (menos la primera)
    const float LabelHeight = 40f;
    const float SliderHeight = 32f;
    const float DropdownHeight = 64f;
    const float ToggleRowHeight = 60f;
    const float ToggleBoxSize = 48f;

    static readonly Color BackgroundColor = new Color(0.07f, 0.09f, 0.13f, 0.97f);
    static readonly Color Surface = new Color(0.16f, 0.21f, 0.28f);           // pistas, cajas, desplegable
    static readonly Color SurfaceHover = new Color(0.24f, 0.31f, 0.41f);
    static readonly Color ListColor = new Color(0.11f, 0.15f, 0.2f);          // lista abierta del desplegable
    static readonly Color TextMain = new Color(0.93f, 0.95f, 0.98f);
    static readonly Color TextSoft = new Color(0.62f, 0.68f, 0.77f);
    static readonly Color Accent = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color AccentPressed = new Color(0.75f, 0.35f, 0.1f);
    static readonly Color AccentLine = new Color(0.95f, 0.47f, 0.17f, 0.35f);

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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image bg = CreateImage(root.transform, "Background", BackgroundColor, null);
        Stretch(bg.rectTransform, 0f);   // tapa el partido cuando se abre desde la pausa y bloquea los clics

        // ---------- Cabecera (arriba, centrada) ----------
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(root.transform, false);
        Place((RectTransform)header.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(ColumnWidth, 120f));
        VerticalLayoutGroup hv = header.AddComponent<VerticalLayoutGroup>();
        hv.childAlignment = TextAnchor.UpperCenter;
        hv.spacing = 4f;
        hv.childControlWidth = hv.childControlHeight = true;
        hv.childForceExpandWidth = hv.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText(header.transform, "Title", "AJUSTES", 84f, FontStyles.Bold, TextMain,
                                           TextAlignmentOptions.Center);
        title.characterSpacing = 6f;
        SetSize(title.gameObject, ColumnWidth, 100f, 0f);

        Image bar = CreateImage(header.transform, "AccentBar", Accent, null);
        SetSize(bar.gameObject, 120f, 6f, 0f);

        // ---------- Columna de opciones (centro) ----------
        GameObject column = new GameObject("Options", typeof(RectTransform));
        column.transform.SetParent(root.transform, false);
        Place((RectTransform)column.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(ColumnWidth, 0f));
        VerticalLayoutGroup cv = column.AddComponent<VerticalLayoutGroup>();
        cv.spacing = ColumnSpacing;
        cv.childControlWidth = cv.childControlHeight = true;
        cv.childForceExpandWidth = true;
        cv.childForceExpandHeight = false;
        column.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateSection(column.transform, "AUDIO", true);
        TextMeshProUGUI musicValue, sfxValue;
        Slider music = CreateSliderBlock(column.transform, "Musica", "Música", uiRes, out musicValue);
        Slider sfx = CreateSliderBlock(column.transform, "Efectos", "Efectos", uiRes, out sfxValue);

        CreateSection(column.transform, "PANTALLA", false);
        TMP_Dropdown dropdown = CreateDropdownBlock(column.transform, "Resolucion", "Resolución", tmpRes);
        Toggle fullscreen = CreateToggleRow(column.transform, "PantallaCompleta", "Pantalla completa");

        CreateSection(column.transform, "JUEGO", false);
        Toggle arrows = CreateToggleRow(column.transform, "FlechasPase", "Mostrar flechas de pase");

        // ---------- Volver (abajo, centrado) ----------
        Button back = CreateButton(root.transform, "Volver", "VOLVER");
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(400f, 90f));

        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "Esc · Volver", 22f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 36f));

        // ---------- Navegación con teclado/mando: de arriba abajo, en el orden visual ----------
        // (izquierda/derecha en los sliders cambian el volumen; eso lo hace el propio Slider)
        Selectable[] order = { music, sfx, dropdown, fullscreen, arrows, back };
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
        Wire(ui, "backButton", back);

        Undo.RegisterCreatedObjectUndo(root, "Crear menú de ajustes");
        EnsureEventSystem();
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[SettingsMenuBuilder] Menú de ajustes creado en la escena '" + root.scene.name + "'.");
    }

    // ---------- Bloques de la columna ----------

    // Título de sección en el color de acento con una línea fina debajo
    static void CreateSection(Transform parent, string text, bool first)
    {
        GameObject block = NewBlock(parent, "Seccion_" + text, 6f, first ? 0 : (int)SectionGap);

        TextMeshProUGUI t = CreateText(block.transform, "Text", text, 24f, FontStyles.Bold, Accent,
                                       TextAlignmentOptions.BottomLeft);
        t.characterSpacing = 8f;
        SetSize(t.gameObject, -1f, 34f, 1f);

        Image line = CreateImage(block.transform, "Line", AccentLine, null);
        SetSize(line.gameObject, -1f, 2f, 1f);
    }

    // Etiqueta y porcentaje en una línea, la pista del slider debajo
    static Slider CreateSliderBlock(Transform parent, string name, string label, DefaultControls.Resources res,
                                    out TextMeshProUGUI valueLabel)
    {
        GameObject block = NewBlock(parent, name, 6f, 0);

        GameObject line = NewRow(block.transform, "Header", LabelHeight, 10f);
        TextMeshProUGUI l = CreateText(line.transform, "Label", label, 32f, FontStyles.Normal, TextMain,
                                       TextAlignmentOptions.MidlineLeft);
        SetSize(l.gameObject, -1f, LabelHeight, 1f);
        valueLabel = CreateText(line.transform, "Value", "80 %", 30f, FontStyles.Bold, Accent,
                                TextAlignmentOptions.MidlineRight);
        SetSize(valueLabel.gameObject, 120f, LabelHeight, 0f);

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

        TextMeshProUGUI l = CreateText(block.transform, "Label", label, 32f, FontStyles.Normal, TextMain,
                                       TextAlignmentOptions.MidlineLeft);
        SetSize(l.gameObject, -1f, LabelHeight, 1f);

        GameObject go = TMP_DefaultControls.CreateDropdown(res);
        go.name = "Dropdown";
        go.transform.SetParent(block.transform, false);
        SetSize(go, -1f, DropdownHeight, 1f);

        TMP_Dropdown dd = go.GetComponent<TMP_Dropdown>();
        dd.image.color = Color.white;               // el color real lo pone el ColorBlock
        dd.colors = ButtonColors(Surface, SurfaceHover, Accent);
        dd.captionText.fontSize = 30f;
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

        TextMeshProUGUI t = CreateText(row.transform, "Label", label, 32f, FontStyles.Normal, TextMain,
                                       TextAlignmentOptions.MidlineLeft);
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