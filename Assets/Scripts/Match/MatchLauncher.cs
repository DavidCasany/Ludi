using UnityEngine;
using UnityEngine.SceneManagement;

// Puente entre escenas para los partidos amistosos (1vs1).
// Quien prepara el partido llama a Launch(setup); la escena del partido lo recoge con TakePending().
public static class MatchLauncher
{
    public const string DefaultMatchScene = "Match";

    static MatchSetup pending;

    public static MatchSetup LastSetup { get; private set; }     // para la revancha
    public static MatchResult LastResult { get; private set; }   // para la pantalla de resultados
    public static string LastScene { get; private set; } = DefaultMatchScene;

    // Con "Enter Play Mode Options" (sin Domain Reload) los static no se reinician solos
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pending = null;
        LastSetup = null;
        LastResult = null;
        LastScene = DefaultMatchScene;
    }

    public static bool Launch(MatchSetup setup, string sceneName = DefaultMatchScene)
    {
        if (setup == null || !setup.IsValid(out string error))
        {
            Debug.LogError("MatchLauncher: setup no válido. " + (setup == null ? "Es null" : error));
            return false;
        }

        pending = setup;
        LastSetup = setup.Clone();
        LastResult = null;
        LastScene = sceneName;
        SceneManager.LoadScene(sceneName);
        return true;
    }

    public static bool Rematch() => LastSetup != null && Launch(LastSetup.Clone(), LastScene);

    // Lo llama la escena del partido. Devuelve null si no hay partido pendiente.
    public static MatchSetup TakePending()
    {
        MatchSetup s = pending;
        pending = null;
        return s;
    }

    public static void ReportResult(MatchResult result) => LastResult = result;
}
