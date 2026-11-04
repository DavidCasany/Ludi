#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// Tools > Ludi > Fonts
//  1. Crear fonts TextMesh Pro ......... crea a Assets/Resources/Fonts/ els Font Assets de les dues fonts
//                                         (Assets/Fonts/*.ttf), els estils Títol i Botó, i posa la de Text
//                                         com a font per defecte de TextMesh Pro (textos nous i UI feta per codi)
//  2. Aplicar a l'escena o prefab obert . canvia la font de tots els TMP de l'escena oberta (o del prefab que
//                                         tinguis obert en mode prefab) segons el seu paper (Títol / Botó / Text)
// Es poden tornar a executar. El pas 2 es pot desfer amb Ctrl+Z.
public static class GameFontsSetup
{
    const string TtfFolder = "Assets/Fonts/";
    const string OutFolder = "Assets/Resources/Fonts/";

    // Tot el que farà servir el joc: així els glifs ja són a l'atles (la resta s'hi afegeix sola si cal)
    const string Charset =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "¡¿«»·ÀÁÈÉÍÏÒÓÚÜÇàáèéíïòóúüçÑñ€ºª×–—’“”…";

    [MenuItem("Tools/Ludi/Fonts/1. Crear fonts TextMesh Pro")]
    static void Create()
    {
        Directory.CreateDirectory(OutFolder);

        TMP_FontAsset display = CreateFontAsset("HoqueiManiacs-Display");
        TMP_FontAsset text = CreateFontAsset("HoqueiManiacs-Text");
        if (display == null || text == null) return;

        // ---------- Estils de la Display ----------
        SavePreset(display, GameFonts.TitleMaterial, m =>
        {
            m.SetFloat("_FaceDilate", 0.12f);
            m.SetColor("_OutlineColor", new Color(0.35f, 0.06f, 0.06f, 1f));   // granat, com el contorn del VS
            m.SetFloat("_OutlineWidth", 0.24f);
            Underlay(m, new Color(0f, 0f, 0f, 0.6f), 0.35f, -0.7f, 0.25f);
        });
        SavePreset(display, GameFonts.ButtonMaterial, m =>
        {
            m.SetFloat("_FaceDilate", 0.05f);
            m.SetColor("_OutlineColor", new Color(0.1f, 0.13f, 0.2f, 1f));     // blau nit, com el menú
            m.SetFloat("_OutlineWidth", 0.18f);
            Underlay(m, new Color(0f, 0f, 0f, 0.5f), 0f, -0.45f, 0.2f);
        });

        // ---------- Font per defecte de TextMesh Pro ----------
        TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            SerializedObject so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Fonts creades a " + OutFolder + ". Ara obre cada escena i fes Tools > Ludi > Fonts > 2. Aplicar a l'escena o prefab obert.");
    }

    static TMP_FontAsset CreateFontAsset(string fileName)
    {
        Font ttf = AssetDatabase.LoadAssetAtPath<Font>(TtfFolder + fileName + ".ttf");
        if (ttf == null)
        {
            EditorUtility.DisplayDialog("Fonts", "No trobo " + TtfFolder + fileName + ".ttf", "D'acord");
            return null;
        }

        string path = OutFolder + fileName + " SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null)
        {
            // Ja existeix: es reaprofita (si s'esborrés, els textos que ja la fan servir perdrien la font)
            existing.TryAddCharacters(Charset, out _);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(ttf, 96, 10, GlyphRenderMode.SDFAA, 2048, 2048,
                                                         AtlasPopulationMode.Dynamic, true);
        fa.name = fileName + " SDF";
        AssetDatabase.CreateAsset(fa, path);

        fa.TryAddCharacters(Charset, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.Log(fileName + ": caràcters que la font no té: " + missing);

        // L'atles i el material han de viure dins del mateix asset (si no, es perden en tancar Unity)
        for (int i = 0; i < fa.atlasTextures.Length; i++)
        {
            fa.atlasTextures[i].name = fa.name + " Atlas" + (i > 0 ? " " + i : "");
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
        }
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    // Variant d'estil: un material que comparteix l'atles de la font. El nom comença pel de la font perquè
    // TextMesh Pro el mostri al desplegable "Material Preset" de l'Inspector.
    static void SavePreset(TMP_FontAsset fa, string name, System.Action<Material> setup)
    {
        string path = OutFolder + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(fa.material) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = fa.material.shader;
            m.CopyPropertiesFromMaterial(fa.material);   // mateix atles; es tornen a posar els valors de l'estil
        }
        setup(m);
        EditorUtility.SetDirty(m);
    }

    static void Underlay(Material m, Color color, float offsetX, float offsetY, float softness)
    {
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", color);
        m.SetFloat("_UnderlayOffsetX", offsetX);
        m.SetFloat("_UnderlayOffsetY", offsetY);
        m.SetFloat("_UnderlaySoftness", softness);
    }

    [MenuItem("Tools/Ludi/Fonts/2. Aplicar a l'escena o prefab obert")]
    static void Apply()
    {
        if (!GameFonts.Available)
        {
            EditorUtility.DisplayDialog("Fonts", "Primer fes Tools > Ludi > Fonts > 1. Crear fonts TextMesh Pro.", "D'acord");
            return;
        }

        TMP_Text[] texts;
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) texts = stage.prefabContentsRoot.GetComponentsInChildren<TMP_Text>(true);
        else texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int title = 0, button = 0, body = 0;
        foreach (TMP_Text t in texts)
        {
            Undo.RecordObject(t, "Fonts del joc");
            GameFonts.Role role = GameFonts.Guess(t);
            GameFonts.Style(t, role);
            if (role == GameFonts.Role.Title) title++;
            else if (role == GameFonts.Role.Button) button++;
            else body++;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            EditorUtility.SetDirty(t);
        }

        if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
        else EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"Fonts aplicades: {title} títols, {button} botons, {body} textos. Desa (Ctrl+S). " +
                  "Si algun text ha quedat amb l'estil equivocat, canvia-li la font a l'Inspector.");
    }
}
#endif
