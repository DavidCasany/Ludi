using UnityEngine;
using UnityEngine.SceneManagement;

// Interruptor del HUD provisional del partido (marcador, barras, textos de OnGUI).
// OnGUI siempre se dibuja ENCIMA de los Canvas, así que cuando una pantalla de Canvas tapa el partido
// (p. ej. el resultado del torneo) hay que esconderlo: GameManager, TeamManager y BulletTime lo miran.
// Vuelve a false solo al cargar otra escena.
public static class HudVisibility
{
    public static bool Hidden { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Init()
    {
        Hidden = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Hidden = false;
    }
}
