using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Pantalla de selección de equipos del 1 VS 1.
//
// Flujo:  elegir LOCAL  ->  elegir VISITANTE  ->  JUGAR
//  - MatchLaunch.Mode (lo pone el menú de inicio) dice si el visitante es la CPU (PvE) o el jugador 2 (PvP).
//  - Al elegir el local se pasa solo al visitante. Clicar un panel lateral vuelve a ese lado.
//  - Un equipo ya elegido por el otro lado no se puede elegir (mismos colores = no se distinguen).
//  - JUGAR rellena un MatchSetup a través de TeamSelectionLauncher; no se carga Match a mano.
//  - Esc: desde el visitante vuelve al local; desde el local vuelve al menú.
public class TeamSelectionUI : MonoBehaviour
{
    enum Side { Home, Away }

    [Header("Equipos (los TeamData de Assets/Data/Teams)")]
    [SerializeField] TeamData[] teams;

    [Header("Rejilla")]
    [Tooltip("Tarjeta desactivada que se copia una vez por equipo")]
    [SerializeField] TeamCard cardTemplate;
    [SerializeField] Transform grid;

    [Header("Lados")]
    [SerializeField] TeamSidePanel homePanel;
    [SerializeField] TeamSidePanel awayPanel;

    [Header("Textos")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text instructionText;
    [SerializeField] TMP_Text errorText;

    [Header("Botones")]
    [SerializeField] Button playButton;
    [SerializeField] Button randomButton;
    [SerializeField] Button backButton;

    [Header("Colores de las etiquetas")]
    [SerializeField] Color homeColor = new Color(0.95f, 0.47f, 0.17f);
    [SerializeField] Color awayColor = new Color(0.3f, 0.6f, 0.95f);

    [Header("Escenas")]
    [SerializeField] string mainMenuScene = "MainMenu";
    [SerializeField] string matchScene = MatchLauncher.DefaultMatchScene;

    readonly List<TeamCard> cards = new List<TeamCard>();
    MatchMode mode;
    Side active;
    TeamData home, away;
    bool leaving;
    bool navigating;   // la última entrada fue teclado/mando (con ratón no se mueve la selección sola)

    bool IsPvP => mode == MatchMode.PvP;
    TeamSidePanel ActivePanel => active == Side.Home ? homePanel : awayPanel;
    TeamData ActiveTeam => active == Side.Home ? home : away;
    TeamData OtherTeam => active == Side.Home ? away : home;

    void Awake()
    {
        playButton.onClick.AddListener(Play);
        // JUGAR se ve apagado hasta que hay dos equipos; sin sello porque es "todavía no", no "no disponible"
        DisabledButtonLook.Ensure(playButton, null);
        DisabledButtonLook.Ensure(randomButton, null);
        DisabledButtonLook.Ensure(backButton, null);
        if (randomButton != null) randomButton.onClick.AddListener(PickRandom);
        backButton.onClick.AddListener(BackToMenu);
        homePanel.Button.onClick.AddListener(() => SetActiveSide(Side.Home));
        awayPanel.Button.onClick.AddListener(() => SetActiveSide(Side.Away));

        BuildCards();
    }

    void Start()
    {
        Time.timeScale = 1f;   // por si venimos de un partido pausado
        mode = MatchLaunch.Mode == MatchMode.PvP ? MatchMode.PvP : MatchMode.PvE;   // aquí Torneo no aplica

        if (titleText != null) titleText.text = IsPvP ? "1 VS 1 · PvP" : "1 VS 1 · PvE";
        homePanel.SetTitle(IsPvP ? "JUGADOR 1" : "TU EQUIPO");
        awayPanel.SetTitle(IsPvP ? "JUGADOR 2" : "RIVAL · CPU");
        ShowError(null);

        SetActiveSide(Side.Home);
    }

    void Update()
    {
        if (leaving) return;

        if (BackPressed())
        {
            if (active == Side.Away) SetActiveSide(Side.Home);
            else BackToMenu();
            return;
        }

        if (PointerMoved()) navigating = false;
        if (!NavigationPressed()) return;
        navigating = true;

        // Con ratón no se selecciona nada al empezar; la primera tecla de navegación selecciona una tarjeta
        EventSystem es = EventSystem.current;
        if (es == null) return;
        GameObject current = es.currentSelectedGameObject;
        if (current == null || !current.activeInHierarchy) SelectBestCard();
    }

    // ---------- Construcción ----------

    void BuildCards()
    {
        cardTemplate.gameObject.SetActive(false);

        HashSet<TeamData> seen = new HashSet<TeamData>();
        foreach (TeamData t in teams ?? new TeamData[0])
        {
            if (t == null || !seen.Add(t)) continue;

            TeamCard card = Instantiate(cardTemplate, grid);
            card.gameObject.SetActive(true);
            card.Setup(t);
            DisabledButtonLook.Ensure(card.Button, null);   // equipo del otro lado: tarjeta gris
            card.Clicked += OnCardClicked;
            card.Focused += OnCardFocused;
            card.Unfocused += OnCardUnfocused;
            cards.Add(card);
        }

        if (cards.Count < 2)
            Debug.LogError("[TeamSelection] Hacen falta al menos 2 equipos en la lista 'teams'.", this);
    }

    // ---------- Elección ----------

    void SetActiveSide(Side side)
    {
        if (leaving) return;
        active = side;
        homePanel.SetHighlighted(side == Side.Home);
        awayPanel.SetHighlighted(side == Side.Away);
        Refresh();

        // Con teclado/mando la selección sigue al lado activo
        if (navigating) SelectBestCard();
    }

    void OnCardClicked(TeamCard card)
    {
        if (leaving || card.Team == OtherTeam) return;
        ShowError(null);

        if (active == Side.Home)
        {
            home = card.Team;
            if (away == null) { SetActiveSide(Side.Away); return; }
        }
        else away = card.Team;

        Refresh();
        if (navigating && home != null && away != null) Select(playButton);
    }

    void PickRandom()
    {
        List<TeamData> options = new List<TeamData>();
        foreach (TeamCard c in cards)
            if (c.Team != OtherTeam && c.Team != ActiveTeam) options.Add(c.Team);
        if (options.Count == 0) return;

        TeamData pick = options[Random.Range(0, options.Count)];
        foreach (TeamCard c in cards)
            if (c.Team == pick) { OnCardClicked(c); return; }
    }

    // Vista previa: el lado activo muestra el equipo bajo el ratón / seleccionado, atenuado
    void OnCardFocused(TeamCard card)
    {
        if (leaving || card.Team == OtherTeam) return;
        ActivePanel.Show(card.Team, card.Team != ActiveTeam);
    }

    void OnCardUnfocused(TeamCard card)
    {
        if (!leaving) ActivePanel.Show(ActiveTeam, false);
    }

    void Refresh()
    {
        string homeLabel = IsPvP ? "J1" : "TÚ";
        string awayLabel = IsPvP ? "J2" : "CPU";

        foreach (TeamCard c in cards)
        {
            if (c.Team == home) c.SetBadge(homeLabel, homeColor);
            else if (c.Team == away) c.SetBadge(awayLabel, awayColor);
            else c.SetBadge(null, Color.clear);

            c.SetAvailable(c.Team != OtherTeam);
        }

        homePanel.Show(home, false);
        awayPanel.Show(away, false);

        playButton.interactable = home != null && away != null && home != away;

        if (instructionText != null)
        {
            if (playButton.interactable)
                instructionText.text = "Pulsa JUGAR para empezar";
            else if (active == Side.Home)
                instructionText.text = IsPvP ? "Jugador 1: elige tu equipo" : "Elige tu equipo";
            else
                instructionText.text = IsPvP ? "Jugador 2: elige tu equipo" : "Elige el equipo rival";
        }
    }

    // ---------- Botones ----------

    void Play()
    {
        if (leaving || !playButton.interactable) return;

        bool ok = IsPvP ? TeamSelectionLauncher.PlayPvP(home, away, matchScene)
                        : TeamSelectionLauncher.PlayPvE(home, away, matchScene);
        if (!ok)
        {
            // Launch ya escribe el motivo en la consola; aquí solo se avisa sin bloquear la pantalla
            ShowError("No se ha podido empezar el partido. Revisa que la escena '" + matchScene +
                      "' esté en Build Profiles.");
            return;
        }
        leaving = true;
        SetButtonsInteractable(false);
    }

    void BackToMenu()
    {
        if (leaving) return;
        if (string.IsNullOrEmpty(mainMenuScene) || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogWarning($"[TeamSelection] No se puede cargar '{mainMenuScene}': revisa el nombre y Build Profiles.", this);
            return;
        }
        leaving = true;
        SetButtonsInteractable(false);
        SceneManager.LoadScene(mainMenuScene);
    }

    void SetButtonsInteractable(bool on)
    {
        playButton.interactable = on;
        backButton.interactable = on;
        if (randomButton != null) randomButton.interactable = on;
        foreach (TeamCard c in cards) c.SetAvailable(on);
    }

    void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        errorText.text = message ?? "";
    }

    // ---------- Selección con teclado / mando ----------

    void SelectBestCard()
    {
        TeamCard best = null;
        foreach (TeamCard c in cards)
        {
            if (!c.Button.interactable) continue;
            if (c.Team == ActiveTeam) { best = c; break; }
            if (best == null) best = c;
        }
        if (best != null) Select(best.Button);
    }

    static void Select(Selectable s)
    {
        if (EventSystem.current != null && s != null) EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    static bool BackPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1);   // B en mando Xbox
#endif
    }

    static bool PointerMoved()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Mouse m = Mouse.current;
        return m != null && (m.delta.ReadValue().sqrMagnitude > 1f || m.leftButton.wasPressedThisFrame);
#else
        return Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.1f ||
               Input.GetMouseButtonDown(0);
#endif
    }

    static bool NavigationPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame ||
                          k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame ||
                          k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
            return true;
        Gamepad g = Gamepad.current;
        return g != null && (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f);
#else
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }
}
