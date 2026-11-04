using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menú Hockey > Muntar capes del jugador
// Crea (o actualitza) dins del prefab Skater la jerarquia de capes que fa servir SkaterVisual,
// hi posa els sprites de Assets/Sprites/Player/ i connecta tots els camps de SkaterVisual.
// Es pot tornar a executar sempre que l'artista entregui sprites nous: no duplica res
// i no mou el BladeTip si ja l'has col·locat a mà.
//
//  Skater
//   └ Visual (SortingGroup)
//      ├ Shadow   -1   player_shadow
//      ├ Base      0   player_base
//      ├ Jersey    1   player_jersey   (color principal)
//      ├ Pattern   2   (el sprite el posa el TeamData)
//      ├ Head      4   player_head
//      └ StickPivot
//         ├ Stick  3   player_stick
//         ├ Gloves 5   player_gloves   (color secundari)
//         └ BladeTip   (on toca la bola)
public static class SkaterVisualBuilder
{
    const string PrefabPath = "Assets/Prefab/Skater.prefab";
    const string SpriteFolder = "Assets/Sprites/Player/";

    [MenuItem("Hockey/Muntar capes del jugador")]
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
            // El sprite antic (el cercle) s'amaga: ara es veuen les capes. No s'esborra, per si cal tornar enrere.
            SpriteRenderer old = root.GetComponent<SpriteRenderer>();
            if (old != null) old.enabled = false;

            Transform visual = Child(root.transform, "Visual");
            ResetLocal(visual);
            if (visual.GetComponent<SortingGroup>() == null) visual.gameObject.AddComponent<SortingGroup>();

            Layer(visual, "Shadow", "player_shadow", -1, missing);
            Layer(visual, "Base", "player_base", 0, missing);
            SpriteRenderer jersey = Layer(visual, "Jersey", "player_jersey", 1, missing);
            SpriteRenderer pattern = Layer(visual, "Pattern", null, 2, missing);
            Layer(visual, "Head", "player_head", 4, missing);

            Transform pivot = Child(visual, "StickPivot");
            ResetLocal(pivot);
            SpriteRenderer stick = Layer(pivot, "Stick", "player_stick", 3, missing);
            SpriteRenderer gloves = Layer(pivot, "Gloves", "player_gloves", 5, missing);

            bool newTip = pivot.Find("BladeTip") == null;
            Transform tip = Child(pivot, "BladeTip");
            if (newTip) tip.localPosition = GuessBladeTip(stick.sprite);

            // Indicador del jugador controlat: anell neó als peus en lloc del quadrat
            SetupControlRing(root, missing);

            // La física no gira: el que gira és Visual
            Rigidbody2D rb = root.GetComponent<Rigidbody2D>();
            if (rb != null) rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

            SkaterVisual sv = root.GetComponent<SkaterVisual>();
            if (sv == null) sv = root.AddComponent<SkaterVisual>();

            SerializedObject so = new SerializedObject(sv);
            so.FindProperty("body").objectReferenceValue = visual;
            so.FindProperty("stickPivot").objectReferenceValue = pivot;
            so.FindProperty("bladeTip").objectReferenceValue = tip;
            so.FindProperty("patternRenderer").objectReferenceValue = pattern;
            SetArray(so.FindProperty("primaryRenderers"), jersey);
            SetArray(so.FindProperty("secondaryRenderers"), gloves);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (missing.Count > 0)
            Debug.LogWarning("Capes muntades, però falten sprites a " + SpriteFolder + ": " + string.Join(", ", missing) +
                             ". Quan arribin, torna a executar Hockey > Muntar capes del jugador.");
        else
            Debug.Log("Capes del jugador muntades a " + PrefabPath + ". Revisa la posició de BladeTip (fill de StickPivot).");
    }

    static void SetupControlRing(GameObject root, List<string> missing)
    {
        Skater skater = root.GetComponent<Skater>();
        GameObject indicator = skater != null ? skater.controlIndicator : null;
        if (indicator == null)
        {
            Transform t = root.transform.Find("Controling");
            if (t != null) indicator = t.gameObject;
        }
        if (indicator == null) return;

        Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "control_ring.png");
        if (ring == null) { missing.Add("control_ring.png"); return; }

        indicator.transform.localPosition = Vector3.zero;
        indicator.transform.localRotation = Quaternion.identity;
        indicator.transform.localScale = Vector3.one;

        SpriteRenderer sr = indicator.GetComponent<SpriteRenderer>();
        if (sr == null) sr = indicator.AddComponent<SpriteRenderer>();
        sr.sprite = ring;
        sr.sortingOrder = -2;   // per sota de tots els jugadors (els seus SortingGroup van a 0)
        if (indicator.GetComponent<ControlRing>() == null) indicator.AddComponent<ControlRing>();
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

        if (spriteName != null)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + spriteName + ".png");
            if (s != null) sr.sprite = s;
            else if (sr.sprite == null) missing.Add(spriteName + ".png");
        }
        return sr;
    }

    static void SetArray(SerializedProperty array, Object value)
    {
        array.arraySize = 1;
        array.GetArrayElementAtIndex(0).objectReferenceValue = value;
    }

    // Primera aproximació: el punt del contorn de l'estic més lluny del centre (la punta de la pala),
    // una mica cap a dins. Després ajusta-ho a mà on toca de debò la bola.
    static Vector3 GuessBladeTip(Sprite s)
    {
        if (s == null) return new Vector3(0.3f, 0.7f, 0f);

        Vector2 best = Vector2.zero;
        foreach (Vector2 v in s.vertices)
            if (v.sqrMagnitude > best.sqrMagnitude) best = v;
        return best * 0.9f;
    }
}
