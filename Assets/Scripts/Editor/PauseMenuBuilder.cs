#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Crea el menú de pausa completo con un clic: Tools > Ludi > Crear menú de pausa
// - Monta toda la jerarquía (Canvas, fondo, panel, botones y confirmación)
// - Conecta todas las referencias de PauseController y PauseMenuUI
// - Lo guarda como prefab en Assets/Prefabs/UI/PauseMenu.prefab y deja una instancia en la escena abierta
// Después puedes cambiar colores, fuentes y sprites a mano en el prefab: el código no depende del aspecto.
public static class PauseMenuBuilder
{
    const string PrefabFolder = "Assets/Prefabs/UI";
    const string PrefabPath = PrefabFolder + "/PauseMenu.prefab";

    static readonly Color DimColor = new Color(0.04f, 0.06f, 0.09f, 0.65f);
    static readonly Color PanelColor = new Color(0.11f, 0.15f, 0.2f, 0.96f);
    static readonly Color TextSoft = new Color(0.75f, 0.8f, 0.88f);
    static readonly Color ButtonNormal = new Color(0.2f, 0.27f, 0.36f);
    static readonly Color ButtonHighlight = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color ButtonPressed = new Color(0.75f, 0.35f, 0.1f);
    static readonly Color ButtonDisabled = new Color(0.2f, 0.27f, 0.36f, 0.4f);

    [MenuItem("Tools/Ludi/Crear menú de pausa")]
    static void Build()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("Menú de pausa", "Ya existe " + PrefabPath + ".\n¿Sobrescribirlo?",
                                         "Sobrescribir", "Cancelar"))
            return;

        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        // ---------- Raíz: Canvas propio, por encima de cualquier otra UI ----------
        GameObject root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas),
                                         typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        PauseController controller = root.AddComponent<PauseController>();
        PauseMenuUI ui = root.AddComponent<PauseMenuUI>();

        // ---------- Fondo oscurecido (tapa la pista y bloquea los clics al juego) ----------
        Image dim = CreateImage(root.transform, "Dim", DimColor, null);
        Stretch(dim.rectTransform);

        // ---------- Panel principal ----------
        Image panel = CreateImage(root.transform, "Panel", PanelColor, rounded);
        Center(panel.rectTransform, 640f);
        CanvasGroup panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        AddVerticalLayout(panel.gameObject, new RectOffset(60, 60, 40, 50), 18f);

        TextMeshProUGUI title = CreateText(panel.transform, "Title", "PAUSA", 72f, FontStyles.Bold, Color.white);
        SetHeight(title.gameObject, 100f);

        TextMeshProUGUI context = CreateText(panel.transform, "Context", "Torneo · Semifinal\nHOME  2 – 1  AWAY",
                                             28f, FontStyles.Normal, TextSoft);
        SetHeight(context.gameObject, 90f);

        Button resume = CreateButton(panel.transform, "Reanudar", "REANUDAR", rounded);
        Button settings = CreateButton(panel.transform, "Ajustes", "AJUSTES", rounded);
        Button bracket = CreateButton(panel.transform, "Cuadro", "VOLVER AL CUADRO", rounded);
        Button quit = CreateButton(panel.transform, "Salir", "SALIR AL MENÚ", rounded);

        // ---------- Confirmación ----------
        GameObject confirm = new GameObject("Confirm", typeof(RectTransform));
        confirm.transform.SetParent(root.transform, false);
        Stretch((RectTransform)confirm.transform);

        Image confirmDim = CreateImage(confirm.transform, "ConfirmDim", new Color(0f, 0f, 0f, 0.5f), null);
        Stretch(confirmDim.rectTransform);

        Image confirmPanel = CreateImage(confirm.transform, "ConfirmPanel", PanelColor, rounded);
        Center(confirmPanel.rectTransform, 820f);
        AddVerticalLayout(confirmPanel.gameObject, new RectOffset(50, 50, 40, 40), 30f);

        TextMeshProUGUI message = CreateText(confirmPanel.transform, "Message", "¿Salir al menú principal?",
                                             32f, FontStyles.Normal, Color.white);
        SetHeight(message.gameObject, 140f);

        GameObject row = new GameObject("Buttons", typeof(RectTransform));
        row.transform.SetParent(confirmPanel.transform, false);
        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20f;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
        SetHeight(row, 90f);

        Button no = CreateButton(row.transform, "No", "CANCELAR", rounded);
        Button yes = CreateButton(row.transform, "Yes", "SALIR", rounded);

        confirm.SetActive(false);

        // ---------- Referencias ----------
        Wire(controller, "ui", ui);
        Wire(ui, "controller", controller);
        Wire(ui, "menuGroup", root.GetComponent<CanvasGroup>());
        Wire(ui, "panelGroup", panelGroup);
        Wire(ui, "contextText", context);
        Wire(ui, "resumeButton", resume);
        Wire(ui, "settingsButton", settings);
        Wire(ui, "bracketButton", bracket);
        Wire(ui, "quitButton", quit);
        Wire(ui, "confirmRoot", confirm);
        Wire(ui, "confirmText", message);
        Wire(ui, "confirmYesLabel", yes.GetComponentInChildren<TextMeshProUGUI>());
        Wire(ui, "confirmYesButton", yes);
        Wire(ui, "confirmNoButton", no);

        // ---------- Prefab ----------
        Undo.RegisterCreatedObjectUndo(root, "Crear menú de pausa");
        EnsureFolder(PrefabFolder);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;

        Debug.Log($"[PauseMenuBuilder] Menú de pausa creado en {PrefabPath} y añadido a la escena '{root.scene.name}'.");
    }

    // ---------- Ayudantes ----------

    static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
        }
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
        t.raycastTarget = false;   // el texto no debe "tapar" el botón
        return t;
    }

    static Button CreateButton(Transform parent, string name, string label, Sprite sprite)
    {
        // Image en blanco: el color real lo pone el ColorBlock del botón (multiplica)
        Image img = CreateImage(parent, name, Color.white, sprite);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;

        ColorBlock c = b.colors;
        c.normalColor = ButtonNormal;
        c.highlightedColor = ButtonHighlight;
        c.selectedColor = ButtonHighlight;
        c.pressedColor = ButtonPressed;
        c.disabledColor = ButtonDisabled;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;

        SetHeight(img.gameObject, 90f);

        TextMeshProUGUI t = CreateText(img.transform, "Text", label, 36f, FontStyles.Bold, Color.white);
        Stretch(t.rectTransform);
        return b;
    }

    static void AddVerticalLayout(GameObject go, RectOffset padding, float spacing)
    {
        VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = padding;
        vlg.spacing = spacing;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // La altura del panel se ajusta sola al contenido (p. ej. al ocultar "Volver al cuadro")
        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    static void SetHeight(GameObject go, float h)
    {
        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = h;
        le.preferredHeight = h;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void Center(RectTransform rt, float width)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, 0f);
    }

    static void Wire(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[PauseMenuBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
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
