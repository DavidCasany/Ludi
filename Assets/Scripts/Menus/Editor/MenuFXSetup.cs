#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ludi > Afegir animacions al menú d'inici
// Amb l'escena MainMenu oberta, afegeix al Preview del menú:
//   FX_VS ........ llum del llamp + lletres VS animades (VsBannerFX) sobre la imatge del 1 VS 1
//   FX_Confetti .. confeti que surt de la copa (ConfettiBurst) sobre la imatge del torneig
//   FX_Attract ... escena viva amb jugadors passant-se la bola (AttractRinkFX) sobre la pista de fons,
//                  que passa a ser la imatge per defecte del menú (sense hover, i amb Ajustes/Crèdits/Sortir)
// Es pot tornar a executar: substitueix només el que crea ell.
public static class MenuFXSetup
{
    const string VsPath = "Assets/Art/UI/bannerVS.png";
    const string BracketPath = "Assets/Art/UI/bannerBraket.png";
    const string GlowPath = "Assets/Art/UI/MenuFX/vs_bolt_glow.png";
    const string LettersPath = "Assets/Art/UI/MenuFX/vs_letters.png";
    const string RinkPath = "Assets/Art/UI/MenuFX/menu_rink.png";
    const string CupPath = "Assets/Art/UI/MenuFX/bracket_cup.png";
    const string BallPath = "Assets/Art/UI/MenuFX/menu_ball.png";
    const string PlayerFolder = "Assets/Sprites/Player/";
    const string TeamsFolder = "Assets/Data/Teams";

    [MenuItem("Tools/Ludi/Afegir animacions al menú d'inici")]
    static void Run()
    {
        MenuPreview preview = Object.FindFirstObjectByType<MenuPreview>(FindObjectsInactive.Include);
        if (preview == null)
        {
            EditorUtility.DisplayDialog("Animacions del menú", "Obre primer l'escena MainMenu.", "D'acord");
            return;
        }

        // Les imatges que fan servir de debò els botons (per si algú les ha canviat)
        Sprite vs = Load(VsPath), bracket = Load(BracketPath);
        foreach (MenuPreviewTrigger t in Object.FindObjectsByType<MenuPreviewTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.Sprite == null) continue;
            string n = t.Sprite.name.ToLowerInvariant();
            if (n.Contains("vs")) vs = t.Sprite;
            else if (n.Contains("brak") || n.Contains("brack")) bracket = t.Sprite;
        }

        Sprite glow = Load(GlowPath), letters = Load(LettersPath);
        if (vs == null || bracket == null || glow == null || letters == null)
        {
            EditorUtility.DisplayDialog("Animacions del menú",
                "Falten imatges: bannerVS, bannerBraket o les de Assets/Art/UI/MenuFX/.", "D'acord");
            return;
        }

        Transform root = preview.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Animacions del menú");

        // ---------- 1 VS 1 ----------
        RectTransform vsFx = Recreate(root, "FX_VS");
        Image glowImg = NewImage(vsFx, "BoltGlow", glow);
        Image lettersImg = NewImage(vsFx, "Letters", letters);
        VsBannerFX vsComp = vsFx.gameObject.AddComponent<VsBannerFX>();
        SerializedObject sv = new SerializedObject(vsComp);
        sv.FindProperty("preview").objectReferenceValue = preview;
        sv.FindProperty("forSprite").objectReferenceValue = vs;
        sv.FindProperty("boltGlow").objectReferenceValue = glowImg;
        sv.FindProperty("letters").objectReferenceValue = lettersImg.rectTransform;
        sv.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Torneig ----------
        RectTransform confetti = Recreate(root, "FX_Confetti");
        // La copa, per sobre del confeti: el confeti neix dins del bol i es veu en sortir-ne
        Sprite cup = Load(CupPath);
        Image cupImg = cup != null ? NewImage(confetti, "CupFront", cup) : null;

        ConfettiBurst cb = confetti.gameObject.AddComponent<ConfettiBurst>();
        SerializedObject sc = new SerializedObject(cb);
        sc.FindProperty("preview").objectReferenceValue = preview;
        sc.FindProperty("forSprite").objectReferenceValue = bracket;
        sc.FindProperty("cupFront").objectReferenceValue = cupImg;
        sc.ApplyModifiedPropertiesWithoutUndo();

        // ---------- Imatge per defecte: pista amb jugadors ----------
        Sprite rink = Load(RinkPath);
        RectTransform attract = null;
        if (rink != null)
        {
            SetDefaultImage(preview, rink);

            attract = Recreate(root, "FX_Attract");
            attract.SetSiblingIndex(vsFx.GetSiblingIndex());   // per sota dels altres efectes
            AttractRinkFX ar = attract.gameObject.AddComponent<AttractRinkFX>();
            SerializedObject sa = new SerializedObject(ar);
            sa.FindProperty("preview").objectReferenceValue = preview;
            sa.FindProperty("forSprite").objectReferenceValue = rink;
            sa.FindProperty("shadowSprite").objectReferenceValue = Load(PlayerFolder + "player_shadow.png");
            sa.FindProperty("baseSprite").objectReferenceValue = Load(PlayerFolder + "player_base.png");
            sa.FindProperty("jerseySprite").objectReferenceValue = Load(PlayerFolder + "player_jersey.png");
            sa.FindProperty("headSprite").objectReferenceValue = Load(PlayerFolder + "player_head.png");
            sa.FindProperty("stickSprite").objectReferenceValue = Load(PlayerFolder + "player_stick.png");
            sa.FindProperty("glovesSprite").objectReferenceValue = Load(PlayerFolder + "player_gloves.png");
            sa.FindProperty("ringSprite").objectReferenceValue = Load(PlayerFolder + "control_ring.png");
            sa.FindProperty("ballSprite").objectReferenceValue = Load(BallPath);

            string[] guids = AssetDatabase.FindAssets("t:TeamData", new[] { TeamsFolder });
            SerializedProperty teams = sa.FindProperty("teams");
            teams.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
                teams.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TeamData>(AssetDatabase.GUIDToAssetPath(guids[i]));
            sa.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("No trobo " + RinkPath + ": la imatge per defecte no canvia.");

        // En l'editor, apagats (el CanvasGroup s'encén sol al Play quan es veu la seva imatge)
        vsFx.GetComponent<CanvasGroup>().alpha = 0f;
        confetti.GetComponent<CanvasGroup>().alpha = 0f;
        if (attract != null) attract.GetComponent<CanvasGroup>().alpha = 0f;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("Animacions del menú afegides (FX_VS i FX_Confetti dins de " + root.name + "). Desa l'escena (Ctrl+S).");
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    // La pista passa a ser la imatge per defecte del MenuPreview (i la que es veu a l'editor)
    static void SetDefaultImage(MenuPreview preview, Sprite sprite)
    {
        SerializedObject so = new SerializedObject(preview);
        so.FindProperty("defaultSprite").objectReferenceValue = sprite;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (string field in new[] { "backImage", "frontImage" })
        {
            Image img = so.FindProperty(field).objectReferenceValue as Image;
            if (img == null) continue;
            Undo.RecordObject(img, "Imatge per defecte");
            img.sprite = sprite;
        }
    }

    static RectTransform Recreate(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Animacions del menú");
        RectTransform rt = (RectTransform)go.transform;
        Stretch(rt);
        rt.SetAsLastSibling();   // per sobre de les imatges del menú
        return rt;
    }

    static Image NewImage(Transform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;   // igual que les imatges del menú: així queden alineades
        img.raycastTarget = false;
        Stretch(img.rectTransform);
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
