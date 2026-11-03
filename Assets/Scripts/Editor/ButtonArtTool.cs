#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Tools > Ludi > Aplicar el marc als botons
// (Opcional: en el juego ButtonArtAutoApply ya lo pone al cargar cada escena. Esto lo deja guardado
// en las escenas para verlo también en el editor sin dar a Play.)
// Pone el marco metálico (Assets/Resources/Art/ButtonFrame.png) a los botones de las escenas de menús
// y del prefab de pausa. Solo toca botones que:
//   - tienen texto (los de icono, como el engranaje, se quedan igual)
//   - usan el sprite por defecto de Unity (UISprite) o ninguno: así no pisa botones con arte propio
//   - no son tarjetas (equipos de la selección, cartas)
// Los botones creados por código (cuadro del torneo, resultado, sorteo, diálogos) ya lo llevan solos.
public static class ButtonArtTool
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/TeamSelection.unity",
        "Assets/Scenes/Configuració.unity",
        "Assets/Scenes/Crèdits.unity",
    };
    const string PausePrefab = "Assets/Prefabs/UI/PauseMenu.prefab";

    [MenuItem("Tools/Ludi/Aplicar el marc als botons")]
    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Marc dels botons", "Sal del modo Play primero.", "OK");
            return;
        }
        if (UIArt.ButtonFrame == null)
        {
            EditorUtility.DisplayDialog("Marc dels botons",
                "No se encuentra Assets/Resources/Art/ButtonFrame.png (o no se ha importado como Sprite).", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string original = SceneManager.GetActiveScene().path;
        List<string> report = new List<string>();

        foreach (string path in Scenes)
        {
            if (!System.IO.File.Exists(path)) { report.Add(path + ": no existe"); continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) n += Skin(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add($"{scene.name}: {n} botones");
        }

        if (System.IO.File.Exists(PausePrefab))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PausePrefab);
            try
            {
                int n = Skin(root);
                PrefabUtility.SaveAsPrefabAsset(root, PausePrefab);
                report.Add($"PauseMenu (prefab): {n} botones");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        if (!string.IsNullOrEmpty(original) && System.IO.File.Exists(original))
            EditorSceneManager.OpenScene(original, OpenSceneMode.Single);

        string msg = string.Join("\n", report);
        Debug.Log("[Marc dels botons]\n" + msg);
        EditorUtility.DisplayDialog("Marc dels botons", msg, "OK");
    }

    static int Skin(GameObject root)
    {
        int n = 0;
        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (!Eligible(b)) continue;
            if (ButtonFrameFitter.Apply(b) != null)
            {
                EditorUtility.SetDirty(b);
                EditorUtility.SetDirty(b.targetGraphic);
                n++;
            }
        }
        return n;
    }

    static bool Eligible(Button b) => ButtonArtAutoApply.Eligible(b);
}
#endif
