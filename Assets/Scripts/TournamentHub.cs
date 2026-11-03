using UnityEngine;
using UnityEngine.SceneManagement;

// Escena del torneo (el "hub"): decide qué se ve; el dibujo lo hace TournamentHubUI (uGUI por código).
//
// Al entrar:
//  - Si llegamos del menú (Nuevo / Continuar) el torneo ya está en memoria.
//  - Si se abre la escena directamente, intenta continuar el guardado; si no hay, ofrece empezar uno.
// Después simula los partidos IA vs IA hasta el siguiente partido del jugador y lo enseña en el cuadro.
// Las mejoras se eligen al terminar cada partido (TournamentResultScreen); aquí solo aparecen si
// quedaron pendientes (p. ej. se cerró el juego a mitad de elección).
//
// Teclado: Enter = JUGAR, Esc = volver al menú (el torneo queda guardado).
public class TournamentHub : MonoBehaviour
{
    [SerializeField] TournamentConfig config;
    [SerializeField] string mainMenuScene = "MainMenu";

    TournamentHubUI ui;
    BracketMatch nextMatch;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1f;
        ui = TournamentHubUI.Create();
        ui.PlayClicked += Play;
        ui.MenuClicked += BackToMenu;
        ui.NewTournamentClicked += NewTournament;

        string error = null;
        if (config == null || !config.IsValid(out error))
        {
            if (error != null) Debug.LogError("[Torneo] " + error, this);
            ui.ShowNoTournament(config, false);
            return;
        }

        bool active = TournamentSession.IsActive && TournamentSession.Config == config;
        if (!active && TournamentSession.HasSave(config)) active = TournamentSession.TryResume(config);

        if (active) Advance();
        else ui.ShowNoTournament(config, true);
    }

    void Advance()
    {
        nextMatch = TournamentSession.AdvanceToPlayerMatch();
        ui.CloseOverlay();
        ui.Render(config, nextMatch);

        if (TournamentSession.Bracket.IsFinished) ui.ShowFinished(config);
        else if (TournamentSession.PlayerPicksPending > 0) ui.ShowDraft(() => ui.Render(config, nextMatch));
    }

    void Update()
    {
        if (leaving || ui == null) return;

        if (UIKit.BackPressed())
        {
            if (ui.HasOverlay && TournamentSession.IsActive && TournamentSession.Bracket.IsFinished) ui.CloseOverlay();
            else if (!ui.HasOverlay) BackToMenu();
            return;
        }
        if (ui.CanPlay && EnterPressed()) Play();
    }

    // ---------- Acciones ----------

    void Play()
    {
        if (leaving || nextMatch == null || TournamentSession.PlayerPicksPending > 0) return;
        if (!SceneReady(config.MatchScene)) return;

        leaving = true;
        MatchLaunch.Mode = MatchMode.Tournament;
        SceneManager.LoadScene(config.MatchScene);
    }

    void NewTournament()
    {
        if (leaving) return;
        TournamentSession.ClearSave();
        if (TournamentSession.StartNew(config)) Advance();
    }

    void BackToMenu()
    {
        if (leaving || !SceneReady(mainMenuScene)) return;
        leaving = true;
        SceneManager.LoadScene(mainMenuScene);
    }

    bool SceneReady(string scene)
    {
        if (!string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene)) return true;
        Debug.LogError($"[Torneo] No se puede cargar la escena '{scene}': añádela a Build Profiles.", this);
        return false;
    }

    static bool EnterPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        var g = UnityEngine.InputSystem.Gamepad.current;
        return (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)) ||
               (g != null && g.startButton.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.JoystickButton7);
#endif
    }
}
