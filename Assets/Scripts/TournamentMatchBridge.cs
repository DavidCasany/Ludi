using UnityEngine;
using UnityEngine.SceneManagement;

// Conecta la escena del partido con el torneo:
//  - Al empezar: pone nombres, colores y nivel de IA de los dos equipos, y activa el gol de oro.
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

        match = TournamentSession.PendingPlayerMatch();
        if (gameManager == null || match == null) enabled = false;
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
        ApplyTeam(gameManager.home, player);
        ApplyTeam(gameManager.away, rival);
    }

    static void ApplyTeam(TeamManager tm, TeamData data)
    {
        foreach (Skater s in tm.skaters)
        {
            SpriteRenderer sr = s.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = data.PrimaryColor;

            SkaterAI ai = s.GetComponent<SkaterAI>();
            if (ai != null) ai.skill = data.AiSkill;
        }

        // El portero restaura su color base tras cada parada, así que se lo cambia él mismo
        if (tm.goalkeeper != null) tm.goalkeeper.SetColor(data.GoalkeeperColor);
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
