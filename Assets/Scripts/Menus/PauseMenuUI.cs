using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

// Parte visual del menú de pausa: mostrar/ocultar, botones, confirmación y texto de contexto.
// El mismo prefab sirve para torneo y 1 VS 1: mira MatchLaunch.Mode al empezar y
//  - en torneo enseña "VOLVER AL CUADRO" y el nombre de la ronda
//  - en 1 VS 1 lo oculta y enseña "PvP" o "PvE"
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] PauseController controller;
    [Tooltip("CanvasGroup de todo el menú (fondo + panel)")]
    [SerializeField] CanvasGroup menuGroup;
    [Tooltip("CanvasGroup del panel de botones (se bloquea mientras está la confirmación)")]
    [SerializeField] CanvasGroup panelGroup;
    [SerializeField] float fadeDuration = 0.12f;

    [Header("Textos")]
    [SerializeField] TMP_Text contextText;

    [Header("Botones")]
    [SerializeField] Button resumeButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button bracketButton;
    [SerializeField] Button quitButton;

    [Header("Confirmación")]
    [SerializeField] GameObject confirmRoot;
    [SerializeField] TMP_Text confirmText;
    [SerializeField] TMP_Text confirmYesLabel;
    [SerializeField] Button confirmYesButton;
    [SerializeField] Button confirmNoButton;

    [Header("Escenas")]
    [SerializeField] string mainMenuScene = "MainMenu";
    [Tooltip("Se carga ENCIMA del partido (aditiva) para no perderlo. Vacío = aún no existe")]
    [SerializeField] string settingsScene = "";

    bool tournament, settingsOpen, leaving;
    Action pendingAction;
    Button returnFocus;
    float targetAlpha;

    void Awake()
    {
        EnsureEventSystem();

        resumeButton.onClick.AddListener(() => controller.Resume());
        settingsButton.onClick.AddListener(OpenSettings);
        bracketButton.onClick.AddListener(AskBracket);
        quitButton.onClick.AddListener(AskQuit);
        confirmYesButton.onClick.AddListener(ConfirmYes);
        confirmNoButton.onClick.AddListener(CloseConfirm);

        confirmRoot.SetActive(false);
        SetMenuVisible(false, true);
    }

    void Start()
    {
        tournament = MatchLaunch.Mode == MatchMode.Tournament;
        bracketButton.gameObject.SetActive(tournament);
    }

    void OnEnable()
    {
        PauseController.PauseChanged += OnPauseChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDisable()
    {
        PauseController.PauseChanged -= OnPauseChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    // Fundido con tiempo real: con Time.timeScale = 0 el tiempo normal no avanza
    void Update()
    {
        if (menuGroup.alpha == targetAlpha) return;
        float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        menuGroup.alpha = Mathf.MoveTowards(menuGroup.alpha, targetAlpha, step);
    }

    // ---------- Abrir / cerrar ----------

    void OnPauseChanged(bool paused)
    {
        confirmRoot.SetActive(false);
        pendingAction = null;
        panelGroup.interactable = true;

        if (paused)
        {
            RefreshContext();
            SetMenuVisible(true, false);
            Select(resumeButton);
        }
        else
        {
            SetMenuVisible(false, false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void SetMenuVisible(bool visible, bool instant)
    {
        targetAlpha = visible ? 1f : 0f;
        if (instant) menuGroup.alpha = targetAlpha;
        menuGroup.interactable = visible;
        menuGroup.blocksRaycasts = visible;
    }

    // Lo llama PauseController al pulsar Esc estando en pausa. Devuelve true si ha usado la tecla.
    public bool HandleBack()
    {
        if (settingsOpen) return true;   // la escena de ajustes gestiona su propio "volver"
        if (confirmRoot.activeSelf)
        {
            CloseConfirm();
            return true;
        }
        return false;
    }

    // ---------- Contexto ----------

    void RefreshContext()
    {
        if (contextText == null) return;

        string mode = tournament ? TournamentLine()
                    : MatchLaunch.Mode == MatchMode.PvP ? "1 VS 1 · PvP" : "1 VS 1 · PvE";

        GameManager gm = GameManager.Instance;
        string score = gm != null ? $"\n{gm.HomeName}  {gm.homeScore} – {gm.awayScore}  {gm.AwayName}" : "";

        contextText.text = mode + score;
    }

    static string TournamentLine()
    {
        BracketMatch m = TournamentSession.PendingPlayerMatch();
        if (m == null) return "Torneig";
        return $"{TournamentSession.Config.TournamentName} · {TournamentSession.Bracket.RoundName(m)}";
    }

    // ---------- Botones ----------

    void AskQuit()
    {
        string msg = tournament
            ? "Vols sortir al menú principal?\nAquest partit no compta: el tornaràs a jugar quan continuïs el torneig."
            : "Vols sortir al menú principal?\nEs perdrà el partit.";
        OpenConfirm(msg, "SORTIR", quitButton, () => LoadScene(mainMenuScene));
    }

    void AskBracket()
    {
        string hub = TournamentSession.Config != null ? TournamentSession.Config.HubScene : "";
        OpenConfirm("Vols tornar al quadre del torneig?\nAquest partit no compta: el tornaràs a jugar.",
                    "ANAR AL QUADRE", bracketButton, () => LoadScene(hub));
    }

    void OpenSettings()
    {
        if (string.IsNullOrEmpty(settingsScene))
        {
            Debug.Log("[Pausa] «Ajustes»: escena sin asignar todavía.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(settingsScene))
        {
            Debug.LogWarning($"[Pausa] La escena '{settingsScene}' no está en la lista de escenas del build.", this);
            return;
        }

        settingsOpen = true;
        SetMenuVisible(false, false);
        SceneManager.LoadSceneAsync(settingsScene, LoadSceneMode.Additive);
    }

    // Cuando la escena de ajustes se descarga (su botón "Volver"), reaparece la pausa
    void OnSceneUnloaded(Scene scene)
    {
        if (!settingsOpen || scene.name != settingsScene) return;
        settingsOpen = false;
        if (!PauseController.IsPaused) return;
        SetMenuVisible(true, false);
        Select(settingsButton);
    }

    // ---------- Confirmación ----------

    void OpenConfirm(string message, string yesLabel, Button from, Action onYes)
    {
        pendingAction = onYes;
        returnFocus = from;
        confirmText.text = message;
        if (confirmYesLabel != null) confirmYesLabel.text = yesLabel;

        confirmRoot.SetActive(true);
        panelGroup.interactable = false;
        Select(confirmNoButton);   // por defecto "Cancelar": pulsar Enter sin querer no te saca del partido
    }

    void CloseConfirm()
    {
        confirmRoot.SetActive(false);
        pendingAction = null;
        panelGroup.interactable = true;
        Select(returnFocus != null ? returnFocus : resumeButton);
    }

    void ConfirmYes()
    {
        Action action = pendingAction;
        pendingAction = null;
        action?.Invoke();
    }

    void LoadScene(string scene)
    {
        if (leaving) return;

        if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[Pausa] No se puede cargar la escena '{scene}': revisa el nombre y la lista de escenas del build.", this);
            CloseConfirm();
            return;
        }

        leaving = true;
        SceneManager.LoadScene(scene);   // PauseController.OnDestroy devuelve el tiempo a 1
    }

    // ---------- Utilidades ----------

    // Con el dedo no se deja nada seleccionado: se vería como pulsado
    static void Select(Selectable s)
    {
        if (InputMode.IsTouch) return;
        if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    // Las escenas de partido usan OnGUI y puede que no tengan EventSystem: sin él los botones no responden
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        go.AddComponent<InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
