#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Ludi > Afegir animacions al menú d'inici
// Amb l'escena MainMenu oberta, afegeix al Preview del menú:
//   FX_VS ........ llum del llamp + lletres VS animades (VsBannerFX) sobre la imatge del 1 VS 1
//   FX_Confetti .. confeti que surt de la copa (ConfettiBurst) sobre la imatge del torneig
// Es pot tornar a executar: substitueix només el que crea ell.
public static class MenuFXSetup
{
    const string VsPath = "Assets/Art/UI/bannerVS.png";
    const string BracketPath = "Assets/Art/UI/bannerBraket.png";
    const string GlowPath = "Assets/Art/UI/MenuFX/vs_bolt_glow.png";
    const string LettersPath = "Assets/Art/UI/MenuFX/vs_letters.png";

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
        ConfettiBurst cb = confetti.gameObject.AddComponent<ConfettiBurst>();
        SerializedObject sc = new SerializedObject(cb);
        sc.FindProperty("preview").objectReferenceValue = preview;
        sc.FindProperty("forSprite").objectReferenceValue = bracket;
        sc.ApplyModifiedPropertiesWithoutUndo();

        // En l'editor, apagats (el CanvasGroup s'encén sol al Play quan es veu la seva imatge)
        vsFx.GetComponent<CanvasGroup>().alpha = 0f;
        confetti.GetComponent<CanvasGroup>().alpha = 0f;

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Debug.Log("Animacions del menú afegides (FX_VS i FX_Confetti dins de " + root.name + "). Desa l'escena (Ctrl+S).");
    }

    static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

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
