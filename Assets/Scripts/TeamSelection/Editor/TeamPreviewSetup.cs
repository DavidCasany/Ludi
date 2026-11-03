#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ludi > Afegir escut i jugador als panells de selecció
// Amb l'escena TeamSelection oberta, afegeix a cada TeamSidePanel (TU EQUIPO / RIVAL):
//   Backdrop (RectMask2D) -> CrestBackground (escut en gran) + Dim (vel negre semitransparent)
//   Content/PlayerPreview -> jugador fet amb les capes de Assets/Sprites/Player/, gran i al mig
// i reordena el panell perquè el protagonista sigui el jugador:
//   títol (TU EQUIPO / RIVAL) fora del panell, a sobre · jugador gran al centre · nom de l'equip a baix
//   s'amaguen l'escut petit, l'abreviació i nivell, i els quadrats de color de Jugadores / Portero
// No reconstrueix l'escena: la resta del que hi hagi es queda igual. Es pot tornar a executar
// (per exemple quan arribin els sprites de l'artista): substitueix només el que crea ell.
// Si algun dia es torna a generar l'escena amb el builder de selecció d'equips, cal tornar-lo a executar.
public static class TeamPreviewSetup
{
    const string SpriteFolder = "Assets/Sprites/Player/";
    const float DimAlpha = 0.6f;

    // Distribució dins del panell (ancorat a dalt; el panell fa 640 d'alt)
    const float TitleAbove = 58f;      // el títol queda fora, per sobre del marc
    const float PlayerTop = -30f;
    const float PlayerSize = 400f;
    const float NameTop = -495f;
    const float TextWidth = 360f;

    [MenuItem("Tools/Ludi/Afegir escut i jugador als panells de selecció")]
    static void Run()
    {
        TeamSidePanel[] panels = Object.FindObjectsByType<TeamSidePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (panels.Length == 0)
        {
            EditorUtility.DisplayDialog("Selecció d'equips", "Obre primer l'escena TeamSelection.", "D'acord");
            return;
        }

        bool missingArt = false;
        foreach (TeamSidePanel panel in panels) missingArt |= Setup(panel);

        EditorSceneManager.MarkSceneDirty(panels[0].gameObject.scene);
        if (missingArt)
            Debug.LogWarning("Panells preparats, però encara no hi ha sprites del jugador a " + SpriteFolder +
                             ". El jugador no es veurà fins que hi siguin: llavors torna a executar aquest menú.");
        else
            Debug.Log("Panells de selecció preparats: escut de fons, vel negre i jugador. Desa l'escena (Ctrl+S).");
    }

    // Retorna true si falten sprites del jugador
    static bool Setup(TeamSidePanel panel)
    {
        Transform holder = panel.transform;
        Undo.RegisterFullObjectHierarchyUndo(holder.gameObject, "Previsualització d'equip");

        Transform body = holder.Find("Body");
        Transform content = holder.Find("Content");
        if (content == null) content = holder;

        // ---------- Fons: escut gran + vel negre, retallats a la mida del panell ----------
        RectTransform backdrop = Recreate(holder, "Backdrop");
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<RectMask2D>();
        backdrop.SetSiblingIndex(body != null ? body.GetSiblingIndex() + 1 : 0);   // per sobre del fons del panell

        float side = ((RectTransform)holder).rect.height * 0.95f;
        Image crestBg = NewImage(backdrop, "CrestBackground", Color.white);
        crestBg.preserveAspect = true;
        crestBg.enabled = false;
        crestBg.rectTransform.sizeDelta = new Vector2(side, side);   // centrat i gran: el panell el retalla

        Image dim = NewImage(backdrop, "Dim", new Color(0f, 0f, 0f, DimAlpha));
        Stretch(dim.rectTransform);

        // ---------- Distribució ----------
        Relayout(holder, content);

        // ---------- Jugador, gran i al mig del panell ----------
        RectTransform root = Recreate(content, "PlayerPreview");
        PlaceTop(root, PlayerTop, new Vector2(PlayerSize, PlayerSize));
        root.SetAsFirstSibling();   // per sota dels textos

        RectTransform bodyRt = NewRect(root, "Body");
        Stretch(bodyRt);

        bool missing = false;
        Layer(bodyRt, "Shadow", "player_shadow", ref missing);
        Layer(bodyRt, "Base", "player_base", ref missing);
        Image jersey = Layer(bodyRt, "Jersey", "player_jersey", ref missing);
        Image pattern = Layer(bodyRt, "Pattern", null, ref missing);
        RectTransform stickPivot = NewRect(bodyRt, "StickPivot");   // l'estic queda per sota del cap...
        Stretch(stickPivot);
        Layer(stickPivot, "Stick", "player_stick", ref missing);
        Layer(bodyRt, "Head", "player_head", ref missing);
        RectTransform glovesPivot = NewRect(bodyRt, "GlovesPivot");  // ...i els guants per sobre
        Stretch(glovesPivot);
        Image gloves = Layer(glovesPivot, "Gloves", "player_gloves", ref missing);

        UIPlayerPreview player = root.gameObject.AddComponent<UIPlayerPreview>();
        SerializedObject sp = new SerializedObject(player);
        sp.FindProperty("jersey").objectReferenceValue = jersey;
        sp.FindProperty("pattern").objectReferenceValue = pattern;
        sp.FindProperty("gloves").objectReferenceValue = gloves;
        sp.FindProperty("body").objectReferenceValue = bodyRt;
        SerializedProperty parts = sp.FindProperty("stickParts");
        parts.arraySize = 2;
        parts.GetArrayElementAtIndex(0).objectReferenceValue = stickPivot;
        parts.GetArrayElementAtIndex(1).objectReferenceValue = glovesPivot;
        sp.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Connexions ----------
        TeamPreviewVisual visual = holder.GetComponent<TeamPreviewVisual>();
        if (visual == null) visual = Undo.AddComponent<TeamPreviewVisual>(holder.gameObject);
        SerializedObject sv = new SerializedObject(visual);
        sv.FindProperty("crestBackground").objectReferenceValue = crestBg;
        sv.FindProperty("player").objectReferenceValue = player;
        sv.FindProperty("dim").objectReferenceValue = dim;
        sv.FindProperty("dimOpacity").floatValue = DimAlpha;
        sv.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject so = new SerializedObject(panel);
        so.FindProperty("teamVisual").objectReferenceValue = visual;
        so.ApplyModifiedPropertiesWithoutUndo();

        return missing;
    }

    // Títol fora, nom a baix, i fora l'escut petit, l'abreviació/nivell i els quadrats de color
    static void Relayout(Transform holder, Transform content)
    {
        RectTransform title = holder.Find("Title") as RectTransform;
        if (title != null) PlaceTop(title, TitleAbove, title.sizeDelta);

        RectTransform nameText = content.Find("Name") as RectTransform;
        if (nameText != null) PlaceTop(nameText, NameTop, new Vector2(TextWidth, 60f));

        // El "?" de quan no hi ha equip, al centre on anirà el jugador
        RectTransform fallback = content.Find("CrestFallback") as RectTransform;
        if (fallback != null) PlaceTop(fallback, PlayerTop - PlayerSize * 0.5f + 120f, new Vector2(TextWidth, 240f));

        // Es desactiven (no s'esborren): TeamSidePanel encara hi escriu i així no dona errors
        foreach (string n in new[] { "Crest", "Info", "PlayerColor", "PlayerColorLabel", "KeeperColor", "KeeperColorLabel" })
        {
            Transform t = content.Find(n);
            if (t != null) t.gameObject.SetActive(false);
        }
    }

    // Ancorat a dalt al centre: 'top' és la distància (negativa = cap avall) des de la vora de dalt
    static void PlaceTop(RectTransform rt, float top, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, top);
        rt.sizeDelta = size;
    }

    // Esborra el fill amb aquest nom (si ja existia d'una execució anterior) i el torna a crear
    static RectTransform Recreate(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        RectTransform rt = NewRect(parent, name);
        Undo.RegisterCreatedObjectUndo(rt.gameObject, "Previsualització d'equip");
        return rt;
    }

    static RectTransform NewRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        RectTransform rt = NewRect(parent, name);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // els clics passen al botó del panell
        return img;
    }

    static Image Layer(Transform parent, string name, string spriteName, ref bool missing)
    {
        Image img = NewImage(parent, name, Color.white);
        Stretch(img.rectTransform);
        img.preserveAspect = true;

        Sprite s = spriteName != null ? AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + spriteName + ".png") : null;
        img.sprite = s;
        img.enabled = s != null;   // una Image sense sprite es veuria com un quadrat blanc
        if (spriteName != null && s == null) missing = true;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
#endif
