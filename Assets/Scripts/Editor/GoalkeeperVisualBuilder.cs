using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menú Hockey > Muntar capes del porter
// Igual que "Muntar capes del jugador", però per al prefab GoalKeeper: hi posa les capes de
// Assets/Sprites/Player/, afegeix GoalkeeperVisual (cos i estic miren la bola) i diu a Goalkeeper
// quines capes ha de pintar amb el color de porter (samarreta i guants) i fer el flaix de les parades.
// El porter no porta patró: va d'un sol color, com als partits de veritat.
// Es pot tornar a executar: no duplica res i no mou el BladeTip si ja l'has col·locat.
public static class GoalkeeperVisualBuilder
{
    const string PrefabPath = "Assets/Prefab/GoalKeeper.prefab";
    const string SpriteFolder = "Assets/Sprites/Player/";
    // Sprites propis del porter (gk_*). Si en falta algun, es fa servir el del jugador.
    const string GkFolder = "Assets/Sprites/Goalkeeper/";

    // S'aplica sol en obrir/compilar Unity si el prefab encara no té les capes del porter
    // (així no cal recordar-se d'executar el menú). Només un cop: després ja troba "Visual/BigGlove".
    [InitializeOnLoadMethod]
    static void AutoBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.transform.Find("Visual/BigGlove") != null) return;
            if (LoadGk("gk_base") == null) return;   // els sprites encara s'estan important
            Build();
        };
    }

    [MenuItem("Hockey/Muntar capes del porter")]
    static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Muntar capes", "No trobo el prefab a " + PrefabPath, "D'acord");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        List<string> missing = new List<string>();
        try
        {
            // El quadrat antic s'amaga (no s'esborra, per si cal tornar enrere)
            SpriteRenderer old = root.GetComponent<SpriteRenderer>();
            if (old != null) old.enabled = false;

            Transform visual = Child(root.transform, "Visual");
            ResetLocal(visual);
            if (visual.GetComponent<SortingGroup>() == null) visual.gameObject.AddComponent<SortingGroup>();

            SpriteRenderer shadow = Layer(visual, "Shadow", "gk_shadow|player_shadow", -1, missing);
            SpriteRenderer baseR = Layer(visual, "Base", "gk_base|player_base", 0, missing);
            SpriteRenderer jersey = Layer(visual, "Jersey", "gk_jersey|player_jersey", 1, missing);
            Layer(visual, "Head", "gk_head|player_head", 4, missing);
            SpriteRenderer bigGlove = Layer(visual, "BigGlove", "gk_glove", 6, missing);   // guant gran (mà lliure)

            Transform pivot = Child(visual, "StickPivot");
            ResetLocal(pivot);
            SpriteRenderer stick = Layer(pivot, "Stick", "player_stick", 3, missing);
            SpriteRenderer gloves = Layer(pivot, "Gloves", "gk_gloves|player_gloves", 5, missing);

            bool newTip = pivot.Find("BladeTip") == null;
            Transform tip = Child(pivot, "BladeTip");
            if (newTip) tip.localPosition = GuessBladeTip(stick.sprite);

            GoalkeeperVisual gv = root.GetComponent<GoalkeeperVisual>();
            if (gv == null) gv = root.AddComponent<GoalkeeperVisual>();
            SerializedObject sv = new SerializedObject(gv);
            sv.FindProperty("body").objectReferenceValue = visual;
            sv.FindProperty("stickPivot").objectReferenceValue = pivot;
            sv.FindProperty("bladeTip").objectReferenceValue = tip;
            // Postura de parada: guardes en papallona, guant estirat i ombra més ampla
            sv.FindProperty("shadowRenderer").objectReferenceValue = shadow;
            sv.FindProperty("baseRenderer").objectReferenceValue = baseR;
            sv.FindProperty("bigGlove").objectReferenceValue = bigGlove;
            sv.FindProperty("shadowIdle").objectReferenceValue = shadow.sprite;
            sv.FindProperty("baseIdle").objectReferenceValue = baseR.sprite;
            sv.FindProperty("gloveIdle").objectReferenceValue = bigGlove.sprite;
            sv.FindProperty("shadowSave").objectReferenceValue = LoadGk("gk_shadow_save");
            sv.FindProperty("baseSave").objectReferenceValue = LoadGk("gk_base_save");
            sv.FindProperty("gloveSave").objectReferenceValue = LoadGk("gk_glove_save");
            sv.ApplyModifiedPropertiesWithoutUndo();

            Goalkeeper gk = root.GetComponent<Goalkeeper>();
            if (gk != null)
            {
                SerializedObject sg = new SerializedObject(gk);
                SerializedProperty kit = sg.FindProperty("kitRenderers");
                kit.arraySize = 3;
                kit.GetArrayElementAtIndex(0).objectReferenceValue = jersey;
                kit.GetArrayElementAtIndex(1).objectReferenceValue = gloves;
                kit.GetArrayElementAtIndex(2).objectReferenceValue = bigGlove;
                sg.ApplyModifiedPropertiesWithoutUndo();

                // Color de prova a l'editor (al partit el posa el TeamData)
                Color c = old != null ? old.color : Color.white;
                jersey.color = gloves.color = bigGlove.color = c;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (missing.Count > 0)
            Debug.LogWarning("Capes del porter muntades, però falten sprites a " + SpriteFolder + ": " + string.Join(", ", missing));
        else
            Debug.Log("Capes del porter muntades a " + PrefabPath + ". Revisa la posició de BladeTip (fill de StickPivot).");
    }

    static Transform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        return t;
    }

    static void ResetLocal(Transform t)
    {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    static SpriteRenderer Layer(Transform parent, string name, string spriteName, int order, List<string> missing)
    {
        Transform t = Child(parent, name);
        ResetLocal(t);

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;

        // "gk_x|player_x": primer el del porter, si no hi és el del jugador
        Sprite s = null;
        string[] options = spriteName.Split('|');
        foreach (string opt in options)
        {
            string folder = opt.StartsWith("gk_") ? GkFolder : SpriteFolder;
            s = AssetDatabase.LoadAssetAtPath<Sprite>(folder + opt + ".png");
            if (s != null) break;
        }
        if (s != null) sr.sprite = s;
        else if (sr.sprite == null) missing.Add(options[options.Length - 1] + ".png");
        return sr;
    }

    static Sprite LoadGk(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(GkFolder + name + ".png");

    static Vector3 GuessBladeTip(Sprite s)
    {
        if (s == null) return new Vector3(0.3f, 0.7f, 0f);
        Vector2 best = Vector2.zero;
        foreach (Vector2 v in s.vertices)
            if (v.sqrMagnitude > best.sqrMagnitude) best = v;
        return best * 0.9f;
    }
}
