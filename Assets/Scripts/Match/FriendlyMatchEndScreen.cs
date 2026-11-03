using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Pantalla final de un amistoso (1vs1): botones REVANCHA y MENÚ encima del
// "FIN DEL PARTIDO" que dibuja el GameManager.
// En el torneo no aparece: allí MatchBootstrapper está desactivado y
// TournamentMatchBridge se encarga de volver al cuadro.
// No choca con la pausa: PauseController no deja pausar con el partido en FullTime.
public class FriendlyMatchEndScreen : MonoBehaviour
{
    [SerializeField] GameManager gameManager;
    [SerializeField] MatchBootstrapper bootstrapper;
    [SerializeField] string mainMenuScene = "MainMenu";

    [Tooltip("Segundos (reales) antes de aceptar input, para no saltarse el resultado con un clic de la jugada")]
    [SerializeField] float inputDelay = 1f;

    bool visible;
    bool loading;
    float shownAt;
    GUIStyle buttonStyle;

    bool Ready => visible && !loading && Time.unscaledTime - shownAt >= inputDelay;

    void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (bootstrapper == null) bootstrapper = FindAnyObjectByType<MatchBootstrapper>();
    }

    void OnEnable()  { if (gameManager != null) gameManager.MatchFinished += OnMatchFinished; }
    void OnDisable() { if (gameManager != null) gameManager.MatchFinished -= OnMatchFinished; }

    void OnMatchFinished(int homeGoals, int awayGoals)
    {
        // Solo amistosos: si el bootstrapper no ha montado este partido, es de torneo
        if (bootstrapper == null || !bootstrapper.enabled || bootstrapper.Setup == null) return;

        visible = true;
        shownAt = Time.unscaledTime;   // unscaled: funciona aunque el tiempo esté ralentizado o parado
    }

    void Update()
    {
        if (!Ready) return;

        if (ConfirmPressed()) Rematch();
        else if (BackPressed()) GoToMenu();
    }

    void OnGUI()
    {
        if (!visible) return;

        GUI.depth = -10;   // menor profundidad = se dibuja encima del OnGUI del GameManager

        if (buttonStyle == null)   // GUI.skin solo existe dentro de OnGUI: se crea una vez aquí
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 32, fontStyle = FontStyle.Bold };

        float w = 260f, h = 70f, gap = 30f;
        float y = Mathf.Min(Screen.height * 0.5f + 230f, Screen.height - h - 20f);

        GUI.enabled = Ready;
        if (GUI.Button(new Rect(Screen.width * 0.5f - w - gap * 0.5f, y, w, h), "REVENJA", buttonStyle)) Rematch();
        if (GUI.Button(new Rect(Screen.width * 0.5f + gap * 0.5f, y, w, h), "MENÚ", buttonStyle)) GoToMenu();
        GUI.enabled = true;
    }

    void Rematch()
    {
        if (loading) return;
        loading = true;
        Time.timeScale = 1f;   // por si el partido acabó en pleno bullet time o QTE

        // Abierta directamente (setup de prueba) no hay LastSetup: se recarga la escena
        if (!MatchLauncher.Rematch())
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        if (loading) return;

        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"FriendlyMatchEndScreen: la escena '{mainMenuScene}' no está en Build Profiles " +
                           "o el nombre no coincide.", this);
            return;
        }

        loading = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuScene);
    }

    // Mismo patrón que PauseController: funciona con el Input System nuevo y con el clásico
    static bool ConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) ||
               (g != null && g.buttonSouth.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.JoystickButton0);   // A en mando de Xbox
#endif
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) ||
               (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) ||
               Input.GetKeyDown(KeyCode.JoystickButton1);   // B en mando de Xbox
#endif
    }
}
