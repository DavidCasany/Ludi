using UnityEngine;
using UnityEngine.SceneManagement;

// Recuerda cuál era la escena anterior (solo cargas normales, no aditivas),
// para que botones como "Volver" sepan adónde ir sin tenerlo escrito a mano.
public static class SceneHistory
{
    public static string Previous { get; private set; } = "";
    static string current = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Previous = "";
        current = "";
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single || scene.name == current) return;   // recargar la misma no cuenta
        Previous = current;
        current = scene.name;
    }
}
