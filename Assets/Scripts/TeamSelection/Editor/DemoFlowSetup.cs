#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Conecta el flujo de menús de la demo de una vez: Tools > Ludi > Configurar flujo de la demo
//
//  MainMenu ──1 VS 1 PvE / PvP──► TeamSelection ──JUGAR──► Match
//      ▲            │                   │                    │
//      └─Ajustes────┘ ◄────VOLVER───────┘ ◄──Salir (pausa)───┘
//
// Qué hace al pulsar "Aplicar":
//  1. Si la escena de inicio no se llama MainMenu (p. ej. "Inici"), ofrece renombrarla.
//  2. Build Profiles: MainMenu primera, luego TeamSelection, Match, Ajustes, Créditos y el resto que ya hubiera.
//  3. MainMenu  -> MainMenuController: pveScene y pvpScene = TeamSelection, settingsScene, creditsScene;
//                  y el botón PvP activado o desactivado para la demo.
//  4. TeamSelection -> TeamSelectionUI: mainMenuScene y matchScene.
//  5. Ajustes   -> SettingsMenuUI: fallbackScene = MainMenu.
//  6. Prefab de pausa (Assets/Prefabs/UI/PauseMenu.prefab) -> mainMenuScene y settingsScene.
//
// NO abre ni modifica Match.unity ni los prefabs del partido. Guarda cada escena que toca y al acabar
// vuelve a abrir la que tenías.
public class DemoFlowSetup : EditorWindow
{
    const string MainMenuName = "MainMenu";
    const string PausePrefabPath = "Assets/Prefabs/UI/PauseMenu.prefab";

    [SerializeField] SceneAsset mainMenu;
    [SerializeField] SceneAsset teamSelection;
    [SerializeField] SceneAsset match;
    [Tooltip("Escena con SettingsMenuUI (opcional)")]
    [SerializeField] SceneAsset settings;
    [Tooltip("Vacío = el botón Créditos solo escribe un aviso")]
    [SerializeField] SceneAsset credits;

    [Tooltip("Desmárcalo si el PvP no llega a la demo: el botón PvP queda desactivado en el menú")]
    [SerializeField] bool pvpEnabled = true;

    readonly List<string> report = new List<string>();
    Vector2 scroll;

    [MenuItem("Tools/Ludi/Configurar flujo de la demo")]
    static void Open()
    {
        DemoFlowSetup w = GetWindow<DemoFlowSetup>(true, "Flujo de la demo");
        w.minSize = new Vector2(420f, 420f);
    }

    void OnEnable()
    {
        if (mainMenu == null) mainMenu = FindScene(MainMenuName, "Inici", "Inicio", "Menu");
        if (teamSelection == null) teamSelection = FindScene(TeamSelectionBuilder.SceneName, "SeleccionEquipos");
        if (match == null) match = FindScene(MatchLauncher.DefaultMatchScene);
        if (settings == null) settings = FindScene("Settings", "Ajustes", "SettingsMenu");
        if (credits == null) credits = FindScene("Credits", "Creditos", "Créditos");
    }

    void OnGUI()
    {
        SerializedObject so = new SerializedObject(this);
        so.Update();
        EditorGUILayout.LabelField("Escenas", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(so.FindProperty("mainMenu"), new GUIContent("Menú de inicio"));
        EditorGUILayout.PropertyField(so.FindProperty("teamSelection"), new GUIContent("Selección de equipos"));
        EditorGUILayout.PropertyField(so.FindProperty("match"), new GUIContent("Partido"));
        EditorGUILayout.PropertyField(so.FindProperty("settings"), new GUIContent("Ajustes"));
        EditorGUILayout.PropertyField(so.FindProperty("credits"), new GUIContent("Créditos"));
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(so.FindProperty("pvpEnabled"), new GUIContent("PvP disponible"));
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Modifica MainMenu, TeamSelection, la escena de Ajustes, el prefab de pausa y Build " +
                                "Profiles. No toca Match.unity. Avisa al equipo si cambia Build Profiles.",
                                MessageType.Info);

        using (new EditorGUI.DisabledScope(mainMenu == null || teamSelection == null || match == null))
        {
            if (GUILayout.Button("Aplicar", GUILayout.Height(36f))) Apply();
        }

        if (report.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string line in report) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }
    }

    // ---------- Aplicar ----------

    void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Flujo de la demo", "Sal del modo Play antes de aplicar.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        report.Clear();
        string originalScene = SceneManager.GetActiveScene().path;

        if (!RenameMainMenuIfNeeded()) return;

        string selectionName = teamSelection.name;
        string settingsName = settings != null ? settings.name : "";
        string creditsName = credits != null ? credits.name : "";

        UpdateBuildProfiles();

        EditScene(mainMenu, scene =>
        {
            MainMenuController menu = Find<MainMenuController>(scene);
            if (menu == null) return "no hay MainMenuController";
            SetString(menu, "pveScene", selectionName);
            SetString(menu, "pvpScene", selectionName);
            SetString(menu, "settingsScene", settingsName);
            SetString(menu, "creditsScene", creditsName);

            Button pvp = GetObject<Button>(menu, "pvpButton");
            if (pvp != null)
            {
                SerializedObject b = new SerializedObject(pvp);
                b.FindProperty("m_Interactable").boolValue = pvpEnabled;
                b.ApplyModifiedPropertiesWithoutUndo();
            }
            return $"PvE/PvP → {selectionName}, Ajustes → {Show(settingsName)}, Créditos → {Show(creditsName)}, " +
                   $"PvP {(pvpEnabled ? "activado" : "desactivado")}";
        });

        EditScene(teamSelection, scene =>
        {
            TeamSelectionUI ui = Find<TeamSelectionUI>(scene);
            if (ui == null) return "no hay TeamSelectionUI (usa Tools > Ludi > Crear pantalla de selección de equipos)";
            SetString(ui, "mainMenuScene", MainMenuName);
            SetString(ui, "matchScene", match.name);
            return $"Volver → {MainMenuName}, Jugar → {match.name}";
        });

        if (settings != null)
        {
            EditScene(settings, scene =>
            {
                SettingsMenuUI ui = Find<SettingsMenuUI>(scene);
                if (ui == null) return "no hay SettingsMenuUI";
                SetString(ui, "fallbackScene", MainMenuName);
                return $"Volver (sin escena anterior) → {MainMenuName}";
            });
        }

        UpdatePausePrefab(settingsName);

        if (!string.IsNullOrEmpty(originalScene) && System.IO.File.Exists(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);

        report.Add("Hecho. Prueba: abre MainMenu, Play, 1 VS 1 · PvE.");
        Debug.Log("[DemoFlowSetup]\n" + string.Join("\n", report));
        Repaint();
    }

    bool RenameMainMenuIfNeeded()
    {
        if (mainMenu.name == MainMenuName) return true;

        string path = AssetDatabase.GetAssetPath(mainMenu);
        if (!EditorUtility.DisplayDialog("Flujo de la demo",
                $"La escena de inicio se llama '{mainMenu.name}'. La pausa y los ajustes vuelven a '{MainMenuName}'.\n\n" +
                $"¿Renombrar {path} a {MainMenuName}.unity?", "Renombrar", "Cancelar"))
            return false;

        string error = AssetDatabase.RenameAsset(path, MainMenuName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Flujo de la demo", "No se ha podido renombrar: " + error, "OK");
            return false;
        }
        AssetDatabase.SaveAssets();
        report.Add($"Escena de inicio renombrada a {MainMenuName}.");
        return true;
    }

    void UpdateBuildProfiles()
    {
        List<string> ordered = new List<string>();
        foreach (SceneAsset s in new[] { mainMenu, teamSelection, match, settings, credits })
        {
            if (s == null) continue;
            string p = AssetDatabase.GetAssetPath(s);
            if (!ordered.Contains(p)) ordered.Add(p);
        }

        List<EditorBuildSettingsScene> result = ordered.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        // Las que ya estaban (hub del torneo, etc.) se mantienen detrás, con su estado
        foreach (EditorBuildSettingsScene old in EditorBuildSettings.scenes)
            if (!ordered.Contains(old.path) && System.IO.File.Exists(old.path)) result.Add(old);

        EditorBuildSettings.scenes = result.ToArray();
        report.Add("Build Profiles: " + string.Join(" · ", result.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
    }

    void UpdatePausePrefab(string settingsName)
    {
        if (!System.IO.File.Exists(PausePrefabPath))
        {
            report.Add("Pausa: no existe " + PausePrefabPath + " (crea el menú con Tools > Ludi > Crear menú de pausa).");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PausePrefabPath);
        try
        {
            PauseMenuUI ui = root.GetComponentInChildren<PauseMenuUI>(true);
            if (ui == null)
            {
                report.Add("Pausa: el prefab no tiene PauseMenuUI.");
                return;
            }
            SetString(ui, "mainMenuScene", MainMenuName);
            SetString(ui, "settingsScene", settingsName);
            PrefabUtility.SaveAsPrefabAsset(root, PausePrefabPath);
            report.Add($"Pausa (prefab): Salir → {MainMenuName}, Ajustes → {Show(settingsName)}. " +
                       "Si la instancia de Match tiene esos campos en negrita (override), revértelos.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------- Utilidades ----------

    void EditScene(SceneAsset asset, System.Func<Scene, string> edit)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        string result = edit(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.Add($"{scene.name}: {result}");
    }

    static T Find<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T c = root.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    static void SetString(Object target, string field, string value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[DemoFlowSetup] No existe el campo '{field}' en {target.GetType().Name}.");
            return;
        }
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T GetObject<T>(Object target, string field) where T : Object
    {
        SerializedProperty p = new SerializedObject(target).FindProperty(field);
        return p != null ? p.objectReferenceValue as T : null;
    }

    static string Show(string scene) => string.IsNullOrEmpty(scene) ? "(sin escena)" : scene;

    static SceneAsset FindScene(params string[] names)
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
        foreach (string n in names)
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == n)
                    return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
        return null;
    }
}
#endif
