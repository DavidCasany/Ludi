using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

// Lógica del menú de inicio: a qué escena va cada botón, estado del botón "Continuar torneo"
// y navegación con teclado/mando. Las escenas se dejan vacías hasta que existan: el botón solo
// escribe un aviso en la consola.
public class MainMenuController : MonoBehaviour
{
    [Header("Escenas (dejar vacío mientras no existan)")]
    [SerializeField] string tournamentScene = "";
    [SerializeField] string pvpScene = "";
    [SerializeField] string pveScene = "";
    [SerializeField] string settingsScene = "";
    [SerializeField] string creditsScene = "";

    [Header("Torneo")]
    [SerializeField] TournamentConfig tournamentConfig;

    [Header("Botones")]
    [SerializeField] Button continueTournamentButton;
    [SerializeField] Button newTournamentButton;
    [SerializeField] Button pvpButton;
    [SerializeField] Button pveButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button creditsButton;
    [SerializeField] Button quitButton;

    [Header("Navegación con teclado / mando")]
    [Tooltip("Filas del menú de arriba abajo: la raíz de cada SplitMenuButton o el botón simple")]
    [SerializeField] GameObject[] rows;
    [SerializeField] bool wrapNavigation = true;

    readonly List<Selectable> rowSelectables = new List<Selectable>();
    bool loading;

    void Awake()
    {
        Bind(continueTournamentButton, ContinueTournament);
        Bind(newTournamentButton, NewTournament);
        Bind(pvpButton, PlayPvP);
        Bind(pveButton, PlayPvE);
        Bind(settingsButton, OpenSettings);
        Bind(creditsButton, OpenCredits);
        Bind(quitButton, Quit);
    }

    void Start()
    {
        Time.timeScale = 1f;   // por si llegamos desde el menú de pausa con el juego parado
        RefreshContinueButton();
        BuildNavigation();     // después de RefreshContinueButton: un botón desactivado no entra en la navegación
    }

    void Update()
    {
        // Con ratón no queremos nada seleccionado al empezar (abriría Torneo solo).
        // La primera vez que se usa teclado/mando sin nada seleccionado, se selecciona la primera fila.
        EventSystem es = EventSystem.current;
        if (loading || es == null || rowSelectables.Count == 0) return;

        GameObject current = es.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;

        if (NavigationPressed()) es.SetSelectedGameObject(rowSelectables[0].gameObject);
    }

    // ---------- Acciones de los botones ----------

    public void ContinueTournament()
    {
        TryLoad(tournamentScene, "Continuar torneo", () => TournamentSession.TryResume(tournamentConfig));
    }

    public void NewTournament()
    {
        TryLoad(tournamentScene, "Nuevo torneo", () =>
        {
            if (tournamentConfig == null)
            {
                Debug.LogError("[MainMenu] Asigna un TournamentConfig al MainMenuController.", this);
                return false;
            }
            TournamentSession.ClearSave();
            return TournamentSession.StartNew(tournamentConfig);
        });
    }

    public void PlayPvP() => TryLoad(pvpScene, "1 VS 1 · PvP", () => { MatchLaunch.Mode = MatchMode.PvP; return true; });
    public void PlayPvE() => TryLoad(pveScene, "1 VS 1 · PvE", () => { MatchLaunch.Mode = MatchMode.PvE; return true; });
    public void OpenSettings() => TryLoad(settingsScene, "Ajustes", null);
    public void OpenCredits() => TryLoad(creditsScene, "Créditos", null);

    public void Quit()
    {
        Debug.Log("[MainMenu] Salir del juego");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Comprueba la escena ANTES de preparar nada: así "Nuevo torneo" no borra la partida guardada
    // si la escena todavía no existe.
    void TryLoad(string scene, string label, Func<bool> prepare)
    {
        if (loading) return;

        if (string.IsNullOrEmpty(scene))
        {
            Debug.Log($"[MainMenu] «{label}»: escena sin asignar todavía.", this);
            return;
        }
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"[MainMenu] «{label}»: la escena '{scene}' no está en la lista de escenas del build " +
                             "o el nombre no coincide.", this);
            return;
        }
        if (prepare != null && !prepare()) return;

        loading = true;
        SceneManager.LoadScene(scene);
    }

    // ---------- Estado ----------

    public void RefreshContinueButton()
    {
        if (continueTournamentButton == null) return;
        bool hasSave = tournamentConfig != null && TournamentSession.HasSave(tournamentConfig);
        continueTournamentButton.interactable = hasSave;
    }

    // ---------- Navegación ----------

    void BuildNavigation()
    {
        rowSelectables.Clear();
        List<SplitMenuButton> splits = new List<SplitMenuButton>();

        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            SplitMenuButton split = row.GetComponent<SplitMenuButton>();
            Selectable sel = split != null ? split.MainButton : row.GetComponent<Selectable>();
            if (sel == null)
            {
                Debug.LogWarning($"[MainMenu] La fila '{row.name}' no tiene ni SplitMenuButton ni Button.", row);
                continue;
            }
            rowSelectables.Add(sel);
            splits.Add(split);
        }

        int n = rowSelectables.Count;
        for (int i = 0; i < n; i++)
        {
            Selectable up = i > 0 ? rowSelectables[i - 1] : (wrapNavigation ? rowSelectables[n - 1] : null);
            Selectable down = i < n - 1 ? rowSelectables[i + 1] : (wrapNavigation ? rowSelectables[0] : null);

            if (splits[i] != null) splits[i].SetVerticalNeighbours(up, down);
            else rowSelectables[i].navigation = SplitMenuButton.ExplicitNav(up, down, null, null);
        }
    }

    static bool NavigationPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard k = Keyboard.current;
        if (k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame ||
                          k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame))
            return true;
        Gamepad g = Gamepad.current;
        return g != null && (g.dpad.ReadValue().sqrMagnitude > 0.25f || g.leftStick.ReadValue().sqrMagnitude > 0.25f);
#else
        return Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.5f || Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.5f;
#endif
    }

    static void Bind(Button b, UnityEngine.Events.UnityAction action)
    {
        if (b != null) b.onClick.AddListener(action);
    }
}
