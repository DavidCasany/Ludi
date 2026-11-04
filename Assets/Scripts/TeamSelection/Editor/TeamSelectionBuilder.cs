#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

// Construye la pantalla de selección de equipos: Tools > Ludi > Crear pantalla de selección de equipos
//  - Ofrece crear (o abrir) la escena Assets/Scenes/TeamSelection.unity
//  - Monta toda la UI con las referencias de TeamSelectionUI, TeamSidePanel y TeamCard conectadas
//  - Rellena la lista de equipos con todos los TeamData de Assets/Data/Teams
//
// Distribución a 1920×1080:
//   arriba ........ 1 VS 1 · PvE + barra de acento + instrucción
//   izquierda ..... panel TU EQUIPO        derecha ..... panel RIVAL · CPU
//   centro ........ rejilla 4 × 2 de tarjetas (se crean al dar Play a partir de una plantilla)
//   abajo ......... VOLVER · ALEATORIO · JUGAR
// Todo son GameObjects normales: después se puede retocar a mano.
public static class TeamSelectionBuilder
{
    public const string SceneName = "TeamSelection";
    public const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
    const string TeamsFolder = "Assets/Data/Teams";
    const string RootName = "TeamSelectionUI";

    const float PanelWidth = 400f;
    const float PanelHeight = 640f;
    const float PanelMargin = 80f;
    const float CardWidth = 200f;
    const float CardHeight = 220f;
    const float CardSpacing = 24f;
    const int Columns = 4;

    static readonly Color BackgroundColor = new Color(0.07f, 0.09f, 0.13f);
    static readonly Color Panel = new Color(0.11f, 0.15f, 0.2f);
    static readonly Color Surface = new Color(0.16f, 0.21f, 0.28f);
    static readonly Color SurfaceHover = new Color(0.24f, 0.31f, 0.41f);
    static readonly Color TextMain = new Color(0.93f, 0.95f, 0.98f);
    static readonly Color TextSoft = new Color(0.62f, 0.68f, 0.77f);
    static readonly Color Accent = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color AccentPressed = new Color(0.75f, 0.35f, 0.1f);

    static Sprite uiSprite;

    [MenuItem("Tools/Ludi/Crear pantalla de selección de equipos")]
    static void Build()
    {
        if (!PrepareScene()) return;

        GameObject old = GameObject.Find(RootName);
        if (old != null &&
            !EditorUtility.DisplayDialog("Selección de equipos",
                "Ya existe '" + RootName + "' en la escena. Se borrará y se creará de nuevo: " +
                "los cambios hechos a mano dentro de él se perderán.", "Sustituir", "Cancelar"))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        if (old != null) Undo.DestroyObjectImmediate(old);

        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---------- Canvas ----------
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                         typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Expand: el lienzo mide al menos 1920×1080 en cualquier formato (móvil 20:9, tableta 4:3...)
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Image bg = CreateImage(root.transform, "Background", BackgroundColor, null);
        Stretch(bg.rectTransform, 0f);

        // ---------- Cabecera ----------
        TextMeshProUGUI title = CreateText(root.transform, "Title", "1 VS 1 · PvE", 72f, FontStyles.Bold, TextMain,
                                           TextAlignmentOptions.Center);
        title.characterSpacing = 6f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(1000f, 90f));

        Image bar = CreateImage(root.transform, "AccentBar", Accent, null);
        Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -146f), new Vector2(120f, 6f));

        TextMeshProUGUI instruction = CreateText(root.transform, "Instruction", "Tria el teu equip", 34f,
                                                 FontStyles.Normal, TextSoft, TextAlignmentOptions.Center);
        Place(instruction.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -166f), new Vector2(1000f, 50f));

        // ---------- Paneles laterales ----------
        TeamSidePanel homePanel = CreateSidePanel(root.transform, "HomePanel", "EL TEU EQUIP",
                                                  new Vector2(0f, 0.5f), new Vector2(PanelMargin, 0f));
        TeamSidePanel awayPanel = CreateSidePanel(root.transform, "AwayPanel", "RIVAL · CPU",
                                                  new Vector2(1f, 0.5f), new Vector2(-PanelMargin, 0f));

        // ---------- Rejilla de equipos ----------
        GameObject gridGO = new GameObject("TeamGrid", typeof(RectTransform));
        gridGO.transform.SetParent(root.transform, false);
        float gridW = Columns * CardWidth + (Columns - 1) * CardSpacing;
        float gridH = 2 * CardHeight + CardSpacing;
        Place((RectTransform)gridGO.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(gridW, gridH));
        GridLayoutGroup grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(CardSpacing, CardSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperCenter;

        TeamCard template = CreateCardTemplate(gridGO.transform);

        // ---------- Botones de abajo ----------
        Button back = CreateButton(root.transform, "Volver", "TORNAR", Surface);
        Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(-460f, 90f), new Vector2(360f, 90f));
        Button random = CreateButton(root.transform, "Aleatorio", "ALEATORI", Surface);
        Place((RectTransform)random.transform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(360f, 90f));
        Button play = CreateButton(root.transform, "Jugar", "JUGAR", Accent);
        Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(460f, 90f), new Vector2(360f, 90f));

        TextMeshProUGUI error = CreateText(root.transform, "Error", "", 28f, FontStyles.Bold,
                                           new Color(1f, 0.45f, 0.4f), TextAlignmentOptions.Center);
        Place(error.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(1400f, 44f));
        error.gameObject.SetActive(false);

        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "Esc · Tornar", 22f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(400f, 36f));

        // ---------- Script ----------
        TeamSelectionUI ui = root.AddComponent<TeamSelectionUI>();
        Wire(ui, "cardTemplate", template);
        Wire(ui, "grid", gridGO.transform);
        Wire(ui, "homePanel", homePanel);
        Wire(ui, "awayPanel", awayPanel);
        Wire(ui, "titleText", title);
        Wire(ui, "instructionText", instruction);
        Wire(ui, "errorText", error);
        Wire(ui, "playButton", play);
        Wire(ui, "randomButton", random);
        Wire(ui, "backButton", back);
        int teamCount = FillTeams(ui);

        Undo.RegisterCreatedObjectUndo(root, "Crear pantalla de selección");
        EnsureEventSystem();
        Undo.CollapseUndoOperations(undoGroup);

        Scene scene = root.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;

        Debug.Log($"[TeamSelectionBuilder] Pantalla creada en '{scene.name}' con {teamCount} equipos. " +
                  "Siguiente paso: Tools > Ludi > Configurar flujo de la demo.");
    }

    // ---------- Escena ----------

    // Crea o abre Assets/Scenes/TeamSelection.unity, o deja construir en la escena abierta
    static bool PrepareScene()
    {
        if (SceneManager.GetActiveScene().name == SceneName) return true;

        bool exists = File.Exists(ScenePath);
        int choice = EditorUtility.DisplayDialogComplex("Selección de equipos",
            exists ? "Ya existe " + ScenePath + ". ¿Abrirla y construir allí la pantalla?"
                   : "Se creará la escena " + ScenePath + " y se construirá allí la pantalla.",
            exists ? "Abrir TeamSelection" : "Crear TeamSelection",
            "Cancelar",
            "Usar la escena abierta");

        if (choice == 1) return false;
        if (choice == 2) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

        if (exists)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
        }
        Light light = Object.FindFirstObjectByType<Light>();
        if (light != null) Object.DestroyImmediate(light.gameObject);   // escena 2D: la luz por defecto sobra

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        return true;
    }

    static int FillTeams(TeamSelectionUI ui)
    {
        List<TeamData> list = new List<TeamData>();
        string[] folders = AssetDatabase.IsValidFolder(TeamsFolder) ? new[] { TeamsFolder } : null;
        foreach (string guid in AssetDatabase.FindAssets("t:TeamData", folders))
        {
            TeamData t = AssetDatabase.LoadAssetAtPath<TeamData>(AssetDatabase.GUIDToAssetPath(guid));
            if (t != null) list.Add(t);
        }
        list.Sort((a, b) => string.Compare(a.TeamName, b.TeamName, System.StringComparison.CurrentCulture));

        SerializedObject so = new SerializedObject(ui);
        SerializedProperty p = so.FindProperty("teams");
        p.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        if (list.Count < 2) Debug.LogWarning("[TeamSelectionBuilder] Se han encontrado " + list.Count +
                                             " equipos en " + TeamsFolder + ". Hacen falta al menos 2.");
        return list.Count;
    }

    // ---------- Panel lateral ----------

    static TeamSidePanel CreateSidePanel(Transform parent, string name, string title, Vector2 anchor, Vector2 pos)
    {
        // Marco de acento (detrás, 6 px más grande) que se enciende en el lado que está eligiendo
        GameObject holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        Place((RectTransform)holder.transform, anchor, pos, new Vector2(PanelWidth, PanelHeight));

        Image frame = CreateImage(holder.transform, "ActiveFrame", Accent, uiSprite);
        frame.type = Image.Type.Sliced;
        frame.raycastTarget = false;
        Stretch(frame.rectTransform, -6f);

        Image body = CreateImage(holder.transform, "Body", Color.white, uiSprite);
        body.type = Image.Type.Sliced;
        Stretch(body.rectTransform, 0f);
        Button button = body.gameObject.AddComponent<Button>();
        button.targetGraphic = body;
        button.colors = ButtonColors(Panel, Surface, Surface);
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;   // con teclado se elige en la rejilla; Esc vuelve al lado local
        button.navigation = nav;

        TextMeshProUGUI titleText = CreateText(holder.transform, "Title", title, 30f, FontStyles.Bold, Accent,
                                               TextAlignmentOptions.Center);
        titleText.characterSpacing = 6f;
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(PanelWidth, 44f));

        // Contenido del equipo (se atenúa en la vista previa)
        GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        contentGO.transform.SetParent(holder.transform, false);
        Stretch((RectTransform)contentGO.transform, 0f);
        CanvasGroup content = contentGO.GetComponent<CanvasGroup>();
        content.blocksRaycasts = false;
        content.interactable = false;

        Image crest = CreateImage(contentGO.transform, "Crest", Color.white, null);
        crest.preserveAspect = true;
        crest.raycastTarget = false;
        Place(crest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(240f, 240f));

        TextMeshProUGUI fallback = CreateText(contentGO.transform, "CrestFallback", "?", 110f, FontStyles.Bold,
                                              TextSoft, TextAlignmentOptions.Center);
        Place(fallback.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(PanelWidth - 40f, 240f));

        TextMeshProUGUI nameText = CreateText(contentGO.transform, "Name", "Sense triar", 40f, FontStyles.Bold,
                                              TextMain, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 24f;
        nameText.fontSizeMax = 40f;
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(PanelWidth - 40f, 60f));

        TextMeshProUGUI info = CreateText(contentGO.transform, "Info", "", 26f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Center);
        Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -424f), new Vector2(PanelWidth - 40f, 40f));

        Image playerColor = CreateSwatch(contentGO.transform, "PlayerColor", "Jugadors", new Vector2(-90f, -500f));
        Image keeperColor = CreateSwatch(contentGO.transform, "KeeperColor", "Porter", new Vector2(90f, -500f));

        TeamSidePanel panel = holder.AddComponent<TeamSidePanel>();
        Wire(panel, "button", button);
        Wire(panel, "activeFrame", frame);
        Wire(panel, "content", content);
        Wire(panel, "titleText", titleText);
        Wire(panel, "crest", crest);
        Wire(panel, "crestFallbackText", fallback);
        Wire(panel, "nameText", nameText);
        Wire(panel, "infoText", info);
        Wire(panel, "playerColor", playerColor);
        Wire(panel, "keeperColor", keeperColor);
        return panel;
    }

    // Cuadrado de color con su etiqueta debajo
    static Image CreateSwatch(Transform parent, string name, string label, Vector2 pos)
    {
        Image swatch = CreateImage(parent, name, Color.white, uiSprite);
        swatch.type = Image.Type.Sliced;
        swatch.raycastTarget = false;
        Place(swatch.rectTransform, new Vector2(0.5f, 1f), pos, new Vector2(70f, 70f));

        TextMeshProUGUI t = CreateText(parent, name + "Label", label, 22f, FontStyles.Normal, TextSoft,
                                       TextAlignmentOptions.Center);
        Place(t.rectTransform, new Vector2(0.5f, 1f), pos + new Vector2(0f, -78f), new Vector2(160f, 30f));
        return swatch;
    }

    // ---------- Tarjeta ----------

    static TeamCard CreateCardTemplate(Transform grid)
    {
        Image bgImg = CreateImage(grid, "CardTemplate", Color.white, uiSprite);
        bgImg.type = Image.Type.Sliced;
        Button button = bgImg.gameObject.AddComponent<Button>();
        button.targetGraphic = bgImg;
        button.colors = ButtonColors(Surface, SurfaceHover, Accent);

        Image stripe = CreateImage(bgImg.transform, "ColorStripe", Color.white, null);
        stripe.raycastTarget = false;
        RectTransform srt = stripe.rectTransform;
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.offsetMin = new Vector2(10f, -24f);
        srt.offsetMax = new Vector2(-10f, -10f);

        Image crest = CreateImage(bgImg.transform, "Crest", Color.white, null);
        crest.preserveAspect = true;
        crest.raycastTarget = false;
        Place(crest.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(120f, 120f));

        TextMeshProUGUI shortName = CreateText(bgImg.transform, "ShortName", "EQU", 32f, FontStyles.Bold, TextMain,
                                               TextAlignmentOptions.Center);
        Place(shortName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(CardWidth - 20f, 44f));

        Image badgeImg = CreateImage(bgImg.transform, "Badge", Accent, uiSprite);
        badgeImg.type = Image.Type.Sliced;
        badgeImg.raycastTarget = false;
        Place(badgeImg.rectTransform, new Vector2(1f, 1f), new Vector2(8f, 12f), new Vector2(80f, 40f));
        TextMeshProUGUI badgeText = CreateText(badgeImg.transform, "Text", "TU", 24f, FontStyles.Bold, Color.white,
                                               TextAlignmentOptions.Center);
        Stretch(badgeText.rectTransform, 0f);

        TeamCard card = bgImg.gameObject.AddComponent<TeamCard>();
        Wire(card, "button", button);
        Wire(card, "crest", crest);
        Wire(card, "colorStripe", stripe);
        Wire(card, "shortNameText", shortName);
        Wire(card, "badge", badgeImg.gameObject);
        Wire(card, "badgeImage", badgeImg);
        Wire(card, "badgeText", badgeText);

        bgImg.gameObject.SetActive(false);   // plantilla: TeamSelectionUI la copia al empezar
        return card;
    }

    // ---------- Ayudantes (mismo estilo que SettingsMenuBuilder) ----------

    static Button CreateButton(Transform parent, string name, string label, Color normal)
    {
        Image img = CreateImage(parent, name, Color.white, uiSprite);
        img.type = Image.Type.Sliced;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.colors = ButtonColors(normal, normal == Accent ? new Color(1f, 0.58f, 0.3f) : Accent,
                                normal == Accent ? new Color(1f, 0.58f, 0.3f) : Accent);

        TextMeshProUGUI t = CreateText(img.transform, "Text", label, 36f, FontStyles.Bold, TextMain,
                                       TextAlignmentOptions.Center);
        t.characterSpacing = 4f;
        Stretch(t.rectTransform, 0f);
        return b;
    }

    static ColorBlock ButtonColors(Color normal, Color highlighted, Color selected)
    {
        ColorBlock c = ColorBlock.defaultColorBlock;
        c.normalColor = normal;
        c.highlightedColor = highlighted;
        c.selectedColor = selected;
        c.pressedColor = AccentPressed;
        c.disabledColor = new Color(normal.r, normal.g, normal.b, 0.35f);
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

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[TeamSelectionBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
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
}
#endif
