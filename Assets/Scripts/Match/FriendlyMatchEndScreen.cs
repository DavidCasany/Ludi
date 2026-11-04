using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    Button rematchButton, menuButton;

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
        BuildButtons();
    }

    // Botons amb el marc de l'art (UIKit.Button), sota el cartell FINAL del marcador (ScoreboardHUD)
    void BuildButtons()
    {
        Canvas canvas = UIKit.CreateCanvas("FriendlyMatchEnd", 60);
        RectTransform row = UIKit.Rect(canvas.transform, "Buttons");
        UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(640f, 96f));

        rematchButton = UIKit.Button(row, "Revenja", "REVENJA", true, 34f);
        UIKit.Place((RectTransform)rematchButton.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f, 96f));
        rematchButton.onClick.AddListener(Rematch);

        menuButton = UIKit.Button(row, "Menu", "MENÚ", false, 34f);
        UIKit.Place((RectTransform)menuButton.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(300f, 96f));
        menuButton.onClick.AddListener(GoToMenu);

        rematchButton.interactable = menuButton.interactable = false;   // s'activen passat inputDelay
    }

    void Update()
    {
        if (rematchButton != null && Ready && !rematchButton.interactable)
        {
            rematchButton.interactable = menuButton.interactable = true;
            UIKit.Select(rematchButton);
        }
        if (!Ready) return;

        if (ConfirmPressed()) Rematch();
        else if (BackPressed()) GoToMenu();
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
