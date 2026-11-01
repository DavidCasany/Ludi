#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

// Construye el menú de inicio en la escena abierta: Tools > Ludi > Crear menú de inicio
// Abre una ventana para elegir sprites y medidas, y al pulsar "Construir" crea toda la jerarquía
// con las referencias de los scripts ya conectadas.
//
// Todo lo que crea son GameObjects normales: después puedes mover, redimensionar y cambiar colores a mano.
// Las proporciones están en pocos sitios (ver "Dónde cambiar las proporciones" en las instrucciones).
public class MainMenuBuilder : EditorWindow
{
    const string RootName = "MainMenuUI";
    const string GearPath = "Assets/Art/UI/Gear.png";

    [Header("Datos")]
    [SerializeField] TournamentConfig tournamentConfig;

    [Header("Imágenes (se pueden poner después)")]
    [SerializeField] Sprite background;
    [Tooltip("Imagen que se ve sin hover, y con Ajustes, Créditos o Salir")]
    [SerializeField] Sprite defaultPreview;
    [SerializeField] Sprite tournamentPreview;
    [SerializeField] Sprite oneVsOnePreview;
    [Tooltip("Vacío = se genera un engranaje blanco en " + GearPath)]
    [SerializeField] Sprite gearIcon;

    [Header("Título")]
    [SerializeField] bool showTitle = true;
    [SerializeField] string titleText = "HOQUEI PATINS";

    [Header("Columna de botones (px a 1920×1080)")]
    [SerializeField] float columnLeft = 160f;
    [SerializeField] float buttonWidth = 480f;
    [SerializeField] float buttonHeight = 90f;
    [SerializeField] float spacing = 20f;

    [Header("Imagen de vista previa")]
    [SerializeField] Vector2 previewSize = new Vector2(880f, 660f);
    [SerializeField] float previewRight = 120f;

    [Header("Botón de ajustes (esquina)")]
    [SerializeField] float gearSize = 110f;
    [SerializeField] float gearMargin = 40f;

    static readonly Color BackgroundColor = new Color(0.07f, 0.09f, 0.13f);
    static readonly Color TextSoft = new Color(0.75f, 0.8f, 0.88f);
    static readonly Color ButtonNormal = new Color(0.2f, 0.27f, 0.36f);
    static readonly Color ButtonHighlight = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color ButtonPressed = new Color(0.75f, 0.35f, 0.1f);
    static readonly Color ButtonDisabled = new Color(0.2f, 0.27f, 0.36f, 0.4f);

    Vector2 scroll;

    [MenuItem("Tools/Ludi/Crear menú de inicio")]
    static void Open()
    {
        MainMenuBuilder w = GetWindow<MainMenuBuilder>(true, "Menú de inicio");
        w.minSize = new Vector2(380f, 520f);
    }

    void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        so.Update();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        SerializedProperty p = so.GetIterator();
        p.NextVisible(true);                                   // salta "m_Script"
        while (p.NextVisible(false)) EditorGUILayout.PropertyField(p, true);
        EditorGUILayout.EndScrollView();

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Se construye en la escena abierta (" + EditorSceneManager.GetActiveScene().name +
                                "). Si ya hay un " + RootName + ", se sustituye (se puede deshacer con Ctrl+Z).",
                                MessageType.Info);
        if (GUILayout.Button("Construir menú", GUILayout.Height(36f))) Build();
    }

    // ---------- Construcción ----------

    void Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null &&
            !EditorUtility.DisplayDialog("Menú de inicio",
                "Ya existe '" + RootName + "' en la escena. Se borrará y se creará de nuevo: " +
                "los cambios hechos a mano dentro de él se perderán.", "Sustituir", "Cancelar"))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        if (old != null) Undo.DestroyObjectImmediate(old);

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite gear = gearIcon != null ? gearIcon : GetOrCreateGearSprite();

        // ---------- Canvas ----------
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                         typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- Fondo ----------
        Image bg = CreateImage(root.transform, "Background", background != null ? Color.white : BackgroundColor,
                               background, false);
        bg.raycastTarget = false;
        Stretch(bg.rectTransform, 0f);

        // ---------- Vista previa (a la derecha) ----------
        GameObject previewGO = new GameObject("Preview", typeof(RectTransform));
        previewGO.transform.SetParent(root.transform, false);
        RectTransform previewRT = (RectTransform)previewGO.transform;
        Place(previewRT, new Vector2(1f, 0.5f), new Vector2(-previewRight, 0f), previewSize);

        Image back = CreatePreviewImage(previewGO.transform, "PreviewBack", defaultPreview);
        Image front = CreatePreviewImage(previewGO.transform, "PreviewFront", defaultPreview);   // debajo = delante

        MenuPreview preview = previewGO.AddComponent<MenuPreview>();
        Wire(preview, "backImage", back);
        Wire(preview, "frontImage", front);
        Wire(preview, "defaultSprite", defaultPreview);

        // ---------- Columna de botones (a la izquierda) ----------
        GameObject column = new GameObject("Buttons", typeof(RectTransform));
        column.transform.SetParent(root.transform, false);
        Place((RectTransform)column.transform, new Vector2(0f, 0.5f), new Vector2(columnLeft, 0f),
              new Vector2(buttonWidth, 0f));

        VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        column.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (showTitle)
        {
            TextMeshProUGUI title = CreateText(column.transform, "Title", titleText, 84f, FontStyles.Bold, Color.white);
            SetHeight(title.gameObject, 160f);
        }

        Button cont, nuevo, pvp, pve;
        GameObject torneo = CreateSplitRow(column.transform, "Torneo", "TORNEO",
                                           "Continuar", "CONTINUAR", "Nuevo", "NUEVO",
                                           tournamentPreview, preview, rounded, out cont, out nuevo);
        GameObject oneVsOne = CreateSplitRow(column.transform, "1vs1", "1 VS 1",
                                             "PvP", "PvP", "PvE", "PvE",
                                             oneVsOnePreview, preview, rounded, out pvp, out pve);

        // "Continuar" se ve apagado cuando no hay partida guardada
        ColorBlock cc = cont.colors;
        cc.disabledColor = ButtonDisabled;
        cont.colors = cc;

        Button creditos = CreateSimpleRow(column.transform, "Creditos", "CRÉDITOS", preview, rounded);
        Button salir = CreateSimpleRow(column.transform, "Salir", "SALIR", preview, rounded);

        // ---------- Ajustes: botón cuadrado en la esquina superior derecha ----------
        Button ajustes = CreateButton(root.transform, "Ajustes", null, rounded);
        Place((RectTransform)ajustes.transform, new Vector2(1f, 1f), new Vector2(-gearMargin, -gearMargin),
              new Vector2(gearSize, gearSize));

        Image icon = CreateImage(ajustes.transform, "Icon", Color.white, gear, false);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Stretch(icon.rectTransform, gearSize * 0.18f);

        MenuPreviewTrigger ajustesTrigger = ajustes.gameObject.AddComponent<MenuPreviewTrigger>();
        Wire(ajustesTrigger, "preview", preview);   // sin sprite = imagen por defecto

        // ---------- Controlador ----------
        GameObject controllerGO = new GameObject("MenuController");
        controllerGO.transform.SetParent(root.transform, false);
        MainMenuController controller = controllerGO.AddComponent<MainMenuController>();

        Wire(controller, "tournamentConfig", tournamentConfig);
        Wire(controller, "continueTournamentButton", cont);
        Wire(controller, "newTournamentButton", nuevo);
        Wire(controller, "pvpButton", pvp);
        Wire(controller, "pveButton", pve);
        Wire(controller, "settingsButton", ajustes);
        Wire(controller, "creditsButton", creditos);
        Wire(controller, "quitButton", salir);
        WireArray(controller, "rows", new Object[] { torneo, oneVsOne, creditos.gameObject, salir.gameObject });

        Undo.RegisterCreatedObjectUndo(root, "Crear menú de inicio");
        EnsureEventSystem();
        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[MainMenuBuilder] Menú de inicio creado en la escena '" + root.scene.name + "'.");

        if (tournamentConfig == null)
            Debug.LogWarning("[MainMenuBuilder] Falta el TournamentConfig: asígnalo en MenuController.", controller);
    }

    // ---------- Filas ----------

    // Raíz (fila) con Main y Split superpuestos, como en el esquema
    GameObject CreateSplitRow(Transform parent, string name, string mainLabel,
                              string leftName, string leftLabel, string rightName, string rightLabel,
                              Sprite previewSprite, MenuPreview preview, Sprite rounded,
                              out Button left, out Button right)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetHeight(row, buttonHeight);

        // Zona de ratón transparente que cubre la fila entera (incluido el hueco entre sub-botones)
        Image hit = row.AddComponent<Image>();
        hit.color = Color.clear;

        Button main = CreateButton(row.transform, "Main", mainLabel, rounded);
        Stretch((RectTransform)main.transform, 0f);
        CanvasGroup mainGroup = main.gameObject.AddComponent<CanvasGroup>();

        GameObject split = new GameObject("Split", typeof(RectTransform));
        split.transform.SetParent(row.transform, false);
        Stretch((RectTransform)split.transform, 0f);
        CanvasGroup splitGroup = split.AddComponent<CanvasGroup>();
        HorizontalLayoutGroup hlg = split.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        left = CreateButton(split.transform, leftName, leftLabel, rounded);
        right = CreateButton(split.transform, rightName, rightLabel, rounded);

        SplitMenuButton smb = row.AddComponent<SplitMenuButton>();
        Wire(smb, "mainButton", main);
        Wire(smb, "mainGroup", mainGroup);
        Wire(smb, "splitGroup", splitGroup);
        WireArray(smb, "subButtons", new Object[] { left, right });

        MenuPreviewTrigger trigger = row.AddComponent<MenuPreviewTrigger>();
        Wire(trigger, "previewSprite", previewSprite);
        Wire(trigger, "preview", preview);

        return row;
    }

    Button CreateSimpleRow(Transform parent, string name, string label, MenuPreview preview, Sprite rounded)
    {
        Button b = CreateButton(parent, name, label, rounded);
        SetHeight(b.gameObject, buttonHeight);
        MenuPreviewTrigger trigger = b.gameObject.AddComponent<MenuPreviewTrigger>();
        Wire(trigger, "preview", preview);   // sin sprite = imagen por defecto
        return b;
    }

    // ---------- Ayudantes de UI ----------

    // Con el sprite por defecto ya puesto, en el editor se ve como quedará (sin él sería un recuadro blanco)
    static Image CreatePreviewImage(Transform parent, string name, Sprite sprite)
    {
        Image img = CreateImage(parent, name, Color.white, sprite, false);
        img.preserveAspect = true;
        img.raycastTarget = false;
        Stretch(img.rectTransform, 0f);
        return img;
    }

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite, bool sliced)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        if (sprite != null && sliced) img.type = Image.Type.Sliced;
        return img;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    // label null = botón sin texto (el de ajustes lleva icono)
    static Button CreateButton(Transform parent, string name, string label, Sprite rounded)
    {
        Image img = CreateImage(parent, name, Color.white, rounded, true);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;

        ColorBlock c = b.colors;
        c.normalColor = ButtonNormal;
        c.highlightedColor = ButtonHighlight;
        c.selectedColor = ButtonHighlight;
        c.pressedColor = ButtonPressed;
        c.disabledColor = ButtonNormal;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;

        if (label != null)
        {
            TextMeshProUGUI t = CreateText(img.transform, "Text", label, 36f, FontStyles.Bold, Color.white);
            Stretch(t.rectTransform, 0f);
        }
        return b;
    }

    static void SetHeight(GameObject go, float h)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;
    }

    // Ancla y pivote en el mismo punto: 'pos' es la distancia a ese borde/esquina
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

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
    }

    // ---------- Referencias ----------

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[MainMenuBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireArray(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null || !p.isArray)
        {
            Debug.LogError($"[MainMenuBuilder] No existe la lista '{field}' en {target.GetType().Name}.");
            return;
        }
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Engranaje generado ----------

    // Dibuja un engranaje blanco de 256×256 con bordes suavizados y lo importa como sprite.
    // Se reutiliza si ya existe; para cambiarlo, sustituye el PNG o asigna tu propio sprite.
    static Sprite GetOrCreateGearSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GearPath);
        if (existing != null) return existing;

        const int S = 256;
        const int teeth = 8;
        float c = (S - 1) * 0.5f;
        float rBody = S * 0.34f, rTooth = S * 0.47f, rHole = S * 0.14f;

        Color32[] px = new Color32[S * S];
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float dx = x - c, dy = y - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Atan2(dy, dx);

            // Posición dentro del periodo de un diente: 0 = centro del diente, 1 = centro del hueco
            float w = Mathf.Repeat(a * teeth / (2f * Mathf.PI), 1f);
            float d = Mathf.Abs(w - 0.5f) * 2f;
            float tooth = Mathf.Clamp01((0.5f - d) / 0.12f + 0.5f);
            float outer = Mathf.Lerp(rBody, rTooth, tooth);

            // Cobertura con 1 px de suavizado en el borde exterior y en el agujero
            float alpha = Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - rHole + 0.5f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
        }

        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();

        EnsureFolder(Path.GetDirectoryName(GearPath).Replace('\\', '/'));
        File.WriteAllBytes(GearPath, tex.EncodeToPNG());
        DestroyImmediate(tex);

        AssetDatabase.ImportAsset(GearPath);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(GearPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(GearPath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
