using UnityEngine;
using UnityEngine.SceneManagement;

// Conecta la escena del partido con el torneo:
//  - Al empezar: pone nombres, equipaciones y nivel de IA de los dos equipos, y activa el gol de oro.
//  - Al terminar: envía el resultado al torneo y muestra "CONTINUAR" para volver al cuadro.
// Si no hay torneo activo (abres la escena Match directamente para probar) no hace nada.
// El jugador humano siempre es el equipo local (GameManager.home).
public class TournamentMatchBridge : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    BracketMatch match;
    bool finished;
    string resultText;
    GUIStyle buttonStyle;

    void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();

        // Abans es comprovava 'match' abans d'assignar-lo (sempre era null) i el pont quedava sempre apagat
        match = TournamentSession.PendingPlayerMatch();
        if (gameManager == null || match == null || MatchLaunch.Mode != MatchMode.Tournament) enabled = false;
    }

    void OnEnable()
    {
        if (gameManager != null) gameManager.MatchFinished += OnMatchFinished;
    }

    void OnDisable()
    {
        if (gameManager != null) gameManager.MatchFinished -= OnMatchFinished;
    }

    void Start()
    {
        TeamData player = TournamentSession.Team(TournamentSession.PlayerTeam);
        TeamData rival = TournamentSession.RivalIn(match);

        gameManager.ConfigureMatch(player.ShortName, rival.ShortName, true);
        TeamDataApplier.ApplyMatch(gameManager.home, player, gameManager.away, rival);   // equipacions + IA

        // Mejoras roguelite de cada equipo (el rival también ha ido eligiendo)
        TeamStatApplier.Apply(gameManager.home, TournamentSession.UpgradesOf(TournamentSession.PlayerTeam));
        TeamStatApplier.Apply(gameManager.away, TournamentSession.UpgradesOf(match.OpponentOf(TournamentSession.PlayerTeam)));
    }

    void OnMatchFinished(int homeGoals, int awayGoals)
    {
        if (finished) return;
        finished = true;

        TournamentSession.ReportPlayerMatch(homeGoals, awayGoals);
        resultText = homeGoals > awayGoals ? "¡Pasas de ronda!"
                   : TournamentSession.PlayerEliminated ? "Eliminado del torneo"
                   : "Primera derrota: sigues vivo en el cuadro de perdedores";
    }

    void OnGUI()
    {
        if (!finished) return;
        GUI.depth = -10;   // por encima del HUD del GameManager

        if (buttonStyle == null)
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };

        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0, Screen.height - 190f, Screen.width, 40f), resultText, label);

        Rect r = new Rect((Screen.width - 280f) * 0.5f, Screen.height - 130f, 280f, 70f);
        if (GUI.Button(r, "CONTINUAR", buttonStyle))
            SceneManager.LoadScene(TournamentSession.Config.HubScene);
    }
}
