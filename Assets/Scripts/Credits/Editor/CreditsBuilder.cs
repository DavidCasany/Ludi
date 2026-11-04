#if UNITY_EDITOR
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

// Construye la pantalla de créditos: Tools > Ludi > Crear pantalla de créditos
//  - Ofrece crear (o abrir) la escena Assets/Scenes/Crèdits.unity
//  - Fondo: una pista de hockey (línea central, círculo, porterías) con una bola rebotando
//  - Centro: la "alineación", una camiseta por miembro del equipo (dorsal, nombre, frase, rol)
//  - Abajo: marcador final EQUIP LUDI 3 – 0 BUGS, "Gràcies per jugar!" y botón TORNAR
// Los textos están en catalán. Los miembros se editan después en CreditsUI > Members.
public static class CreditsBuilder
{
    public const string SceneName = "Crèdits";
    public const string ScenePath = "Assets/Scenes/" + SceneName + ".unity";
    const string RootName = "CreditsUI";
    const string RingPath = "Assets/Art/UI/RinkRing.png";

    const float CardWidth = 440f;
    const float CardHeight = 520f;
    const float CardSpacing = 60f;
    const float BandHeight = 150f;

    static readonly Color BackgroundColor = new Color(0.07f, 0.09f, 0.13f);
    static readonly Color RinkColor = new Color(0.09f, 0.12f, 0.17f);
    static readonly Color RinkLine = new Color(1f, 1f, 1f, 0.07f);
    static readonly Color Panel = new Color(0.11f, 0.15f, 0.2f);
    static readonly Color Surface = new Color(0.16f, 0.21f, 0.28f);
    static readonly Color TextMain = new Color(0.93f, 0.95f, 0.98f);
    static readonly Color TextSoft = new Color(0.62f, 0.68f, 0.77f);
    static readonly Color Accent = new Color(0.95f, 0.47f, 0.17f);
    static readonly Color AccentPressed = new Color(0.75f, 0.35f, 0.1f);
    static readonly Color Red = new Color(0.92f, 0.3f, 0.3f);
    static readonly Color Blue = new Color(0.3f, 0.6f, 0.95f);
    static readonly Color Green = new Color(0.35f, 0.8f, 0.5f);

    static Sprite uiSprite, knobSprite;

    [MenuItem("Tools/Ludi/Crear pantalla de créditos")]
    static void Build()
    {
        if (!PrepareScene()) return;

        GameObject old = GameObject.Find(RootName);
        if (old != null &&
            !EditorUtility.DisplayDialog("Crèdits",
                "Ya existe '" + RootName + "' en la escena. Se borrará y se creará de nuevo: " +
                "los cambios hechos a mano dentro de él se perderán.", "Sustituir", "Cancelar"))
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        if (old != null) Undo.DestroyObjectImmediate(old);

        uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        Sprite ring = GetOrCreateRingSprite();

        // ---------- Canvas ----------
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                         typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Expand: el lienzo mide al menos 1920×1080 en cualquier formato (móvil 20:9, tableta 4:3...)
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        Image bg = CreateImage(root.transform, "Background", BackgroundColor, null);
        Stretch(bg.rectTransform, 0f);

        // ---------- Pista de fondo ----------
        Image border = CreateImage(root.transform, "RinkBorder", RinkLine, uiSprite);
        border.type = Image.Type.Sliced;
        Stretch(border.rectTransform, 34f);
        Image rink = CreateImage(root.transform, "Rink", RinkColor, uiSprite);
        rink.type = Image.Type.Sliced;
        Stretch(rink.rectTransform, 40f);

        Image centerLine = CreateImage(rink.transform, "CenterLine", RinkLine, null);
        centerLine.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        centerLine.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        centerLine.rectTransform.sizeDelta = new Vector2(6f, 0f);

        Image circle = CreateImage(rink.transform, "CenterCircle", RinkLine, ring);
        Place(circle.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 340f));
        Image dot = CreateImage(rink.transform, "CenterDot", RinkLine, knobSprite);
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

        Image goalL = CreateImage(rink.transform, "GoalLeft", RinkLine, uiSprite);
        goalL.type = Image.Type.Sliced;
        Place(goalL.rectTransform, new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(22f, 170f));
        Image goalR = CreateImage(rink.transform, "GoalRight", RinkLine, uiSprite);
        goalR.type = Image.Type.Sliced;
        Place(goalR.rectTransform, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(22f, 170f));

        Image areaL = CreateImage(rink.transform, "AreaLeft", RinkLine, ring);
        Place(areaL.rectTransform, new Vector2(0f, 0.5f), new Vector2(-130f, 0f), new Vector2(420f, 420f));
        Image areaR = CreateImage(rink.transform, "AreaRight", RinkLine, ring);
        Place(areaR.rectTransform, new Vector2(1f, 0.5f), new Vector2(130f, 0f), new Vector2(420f, 420f));
        rink.gameObject.AddComponent<RectMask2D>();   // las áreas son medio círculo: se recortan con la pista

        foreach (Graphic g in rink.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        rink.raycastTarget = border.raycastTarget = false;

        Image ball = CreateImage(rink.transform, "Ball", new Color(1f, 0.55f, 0.2f, 0.9f), knobSprite);
        ball.raycastTarget = false;
        Place(ball.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-300f, 120f), new Vector2(34f, 34f));

        // ---------- Cabecera ----------
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(CanvasGroup));
        header.transform.SetParent(root.transform, false);
        Place((RectTransform)header.transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 180f));

        TextMeshProUGUI title = CreateText(header.transform, "Title", "CRÈDITS", 104f, FontStyles.Bold, TextMain,
                                           TextAlignmentOptions.Center);
        title.characterSpacing = 10f;
        Place(title.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200f, 120f));

        TextMeshProUGUI subtitle = CreateText(header.transform, "Subtitle", "L'ALINEACIÓ", 30f, FontStyles.Bold, Accent,
                                              TextAlignmentOptions.Center);
        subtitle.characterSpacing = 14f;
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(1200f, 44f));

        // ---------- Alineación ----------
        GameObject row = new GameObject("Lineup", typeof(RectTransform));
        row.transform.SetParent(root.transform, false);
        Place((RectTransform)row.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1800f, CardHeight));
        HorizontalLayoutGroup h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = CardSpacing;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;   // tamaño fijo: la escala/rotación la anima la carta
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        CreditCard template = CreateCardTemplate(row.transform);

        // ---------- Pie: marcador y agradecimiento ----------
        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(CanvasGroup));
        footer.transform.SetParent(root.transform, false);
        Stretch((RectTransform)footer.transform, 0f);
        footer.GetComponent<CanvasGroup>().blocksRaycasts = false;

        Image box = CreateImage(footer.transform, "Scoreboard", Panel, uiSprite);
        box.type = Image.Type.Sliced;
        box.raycastTarget = false;
        Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(780f, 120f));
        box.rectTransform.pivot = new Vector2(0.5f, 0.5f);          // el "golpe" de escala desde el centro
        box.rectTransform.anchoredPosition = new Vector2(0f, 230f);

        TextMeshProUGUI boxLabel = CreateText(box.transform, "Label", "RESULTAT FINAL", 20f, FontStyles.Bold, Accent,
                                              TextAlignmentOptions.Center);
        boxLabel.characterSpacing = 10f;
        Place(boxLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(760f, 28f));

        TextMeshProUGUI score = CreateText(box.transform, "Score", "EQUIP LUDI   <b>3 – 0</b>   BUGS", 48f,
                                           FontStyles.Normal, TextMain, TextAlignmentOptions.Center);
        Place(score.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(760f, 70f));

        TextMeshProUGUI thanks = CreateText(footer.transform, "Thanks", "Gràcies per jugar!", 32f, FontStyles.Italic,
                                            TextMain, TextAlignmentOptions.Center);
        Place(thanks.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(900f, 44f));

        TextMeshProUGUI madeWith = CreateText(footer.transform, "MadeWith", "Fet amb Unity · Projecte per als Premis Ludi",
                                              22f, FontStyles.Normal, TextSoft, TextAlignmentOptions.Right);
        Place(madeWith.rectTransform, new Vector2(1f, 0f), new Vector2(-70f, 64f), new Vector2(700f, 34f));

        // ---------- Tornar ----------
        Button back = CreateButton(root.transform, "Tornar", "TORNAR");
        Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(70f, 56f), new Vector2(300f, 80f));

        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "Esc · Tornar", 22f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(400f, 34f));

        // ---------- Script ----------
        CreditsUI ui = root.AddComponent<CreditsUI>();
        Wire(ui, "cardTemplate", template);
        Wire(ui, "cardRow", row.transform);
        Wire(ui, "header", header.transform);
        Wire(ui, "headerGroup", header.GetComponent<CanvasGroup>());
        Wire(ui, "footerGroup", footer.GetComponent<CanvasGroup>());
        Wire(ui, "scoreText", score);
        Wire(ui, "scoreBox", box.rectTransform);
        Wire(ui, "ball", ball.rectTransform);
        Wire(ui, "ballArea", rink.rectTransform);
        Wire(ui, "backButton", back);
        FillMembers(ui);

        Undo.RegisterCreatedObjectUndo(root, "Crear pantalla de créditos");
        EnsureEventSystem();
        Undo.CollapseUndoOperations(undoGroup);

        Scene scene = root.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;
        Debug.Log($"[CreditsBuilder] Pantalla de créditos creada en '{scene.name}'. " +
                  "Siguiente paso: Tools > Ludi > Configurar flujo de la demo.");
    }

    // ---------- Equipo (textos en catalán) ----------

    static void FillMembers(CreditsUI ui)
    {
        var data = new[]
        {
            (name: "Gerard Canadell Xandri", number: 5, tagline: "Posa el color a la pista",
             role: "Direcció d'art\nDisseny de personatges\nEstil visual del joc", color: Red),
            (name: "Àlex Tarazaga Carcaré", number: 1, tagline: "L'entrenador de tots els rivals",
             role: "Programació principal\nIA dels rivals\nMecàniques i sistemes de joc", color: Blue),
            (name: "David Casany Ballesté", number: 8, tagline: "Del menú a la final del torneig",
             role: "Disseny i implementació de la UI\nDisseny sonor\nProgramació del mode torneig", color: Green),
        };

        SerializedObject so = new SerializedObject(ui);
        SerializedProperty list = so.FindProperty("members");
        list.arraySize = data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            SerializedProperty m = list.GetArrayElementAtIndex(i);
            m.FindPropertyRelative("fullName").stringValue = data[i].name;
            m.FindPropertyRelative("number").intValue = data[i].number;
            m.FindPropertyRelative("tagline").stringValue = data[i].tagline;
            m.FindPropertyRelative("role").stringValue = data[i].role;
            m.FindPropertyRelative("color").colorValue = data[i].color;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Carta (camiseta) ----------

    static CreditCard CreateCardTemplate(Transform parent)
    {
        GameObject card = new GameObject("CardTemplate", typeof(RectTransform), typeof(CanvasGroup));
        card.transform.SetParent(parent, false);
        ((RectTransform)card.transform).sizeDelta = new Vector2(CardWidth, CardHeight);

        Image body = CreateImage(card.transform, "Body", Surface, uiSprite);
        body.type = Image.Type.Sliced;
        Stretch(body.rectTransform, 0f);   // raycastTarget activo: recibe el ratón para el efecto hover

        Image band = CreateImage(card.transform, "Band", Accent, uiSprite);
        band.type = Image.Type.Sliced;
        band.raycastTarget = false;
        RectTransform brt = band.rectTransform;
        brt.anchorMin = new Vector2(0f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(0f, BandHeight);
        brt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI number = CreateText(band.transform, "Number", "7", 120f, FontStyles.Bold,
                                            new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center);
        Stretch(number.rectTransform, 0f);

        TextMeshProUGUI nameText = CreateText(card.transform, "Name", "Nom Cognom Cognom", 42f, FontStyles.Bold,
                                              TextMain, TextAlignmentOptions.Center);
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 28f;
        nameText.fontSizeMax = 42f;
        Place(nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BandHeight - 18f),
              new Vector2(CardWidth - 50f, 110f));

        TextMeshProUGUI tagline = CreateText(card.transform, "Tagline", "Frase", 26f, FontStyles.Italic, Accent,
                                             TextAlignmentOptions.Center);
        tagline.enableAutoSizing = true;
        tagline.fontSizeMin = 18f;
        tagline.fontSizeMax = 26f;
        Place(tagline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BandHeight - 136f),
              new Vector2(CardWidth - 40f, 40f));

        Image divider = CreateImage(card.transform, "Divider", new Color(1f, 1f, 1f, 0.12f), null);
        divider.raycastTarget = false;
        Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BandHeight - 192f), new Vector2(90f, 4f));

        TextMeshProUGUI role = CreateText(card.transform, "Role", "Rol", 26f, FontStyles.Normal, TextSoft,
                                          TextAlignmentOptions.Top);
        role.lineSpacing = 12f;
        role.enableAutoSizing = true;
        role.fontSizeMin = 18f;
        role.fontSizeMax = 26f;
        Place(role.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -BandHeight - 214f),
              new Vector2(CardWidth - 50f, CardHeight - BandHeight - 234f));

        CreditCard c = card.AddComponent<CreditCard>();
        Wire(c, "band", band);
        Wire(c, "numberText", number);
        Wire(c, "nameText", nameText);
        Wire(c, "taglineText", tagline);
        Wire(c, "roleText", role);
        Wire(c, "group", card.GetComponent<CanvasGroup>());

        card.SetActive(false);   // plantilla: CreditsUI la copia una vez por miembro
        return c;
    }

    // ---------- Escena ----------

    static bool PrepareScene()
    {
        if (SceneManager.GetActiveScene().name == SceneName) return true;

        bool exists = File.Exists(ScenePath);
        int choice = EditorUtility.DisplayDialogComplex("Crèdits",
            exists ? "Ya existe " + ScenePath + ". ¿Abrirla y construir allí la pantalla?"
                   : "Se creará la escena " + ScenePath + " y se construirá allí la pantalla.",
            exists ? "Abrir Crèdits" : "Crear Crèdits",
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
        if (light != null) Object.DestroyImmediate(light.gameObject);

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        return true;
    }

    // Anillo blanco con borde suave (círculo central y áreas de la pista). Se guarda una vez como asset.
    static Sprite GetOrCreateRingSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
        if (existing != null) return existing;

        const int size = 256;
        const float thickness = 7f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f, outer = c - 1f, inner = outer - thickness;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();

        string folder = Path.GetDirectoryName(RingPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }
        File.WriteAllBytes(RingPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(RingPath);

        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(RingPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(RingPath);
    }

    // ---------- Ayudantes (mismo estilo que los otros builders) ----------

    static Button CreateButton(Transform parent, string name, string label)
    {
        Image img = CreateImage(parent, name, Color.white, uiSprite);
        img.type = Image.Type.Sliced;
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock c = ColorBlock.defaultColorBlock;
        c.normalColor = Surface;
        c.highlightedColor = Accent;
        c.selectedColor = Accent;
        c.pressedColor = AccentPressed;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        b.colors = c;

        TextMeshProUGUI t = CreateText(img.transform, "Text", label, 34f, FontStyles.Bold, TextMain,
                                       TextAlignmentOptions.Center);
        t.characterSpacing = 4f;
        Stretch(t.rectTransform, 0f);
        return b;
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
            Debug.LogError($"[CreditsBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
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
