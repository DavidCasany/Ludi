using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Escena "hub" del torneo: menú de inicio, cuadro de doble eliminación, resultados simulados
// y botón para jugar el siguiente partido. Dibujado con OnGUI como el resto del HUD provisional;
// cuando tengáis el arte, se cambia por UI Toolkit/Canvas leyendo los mismos datos de TournamentSession.
//
// Aquí irá también la pantalla de gestión entre partidos (vestuario), entre el cuadro y "JUGAR".
public class TournamentHub : MonoBehaviour
{
    [SerializeField] TournamentConfig config;

    [Header("Cuadro")]
    [SerializeField] float columnWidth = 190f;
    [SerializeField] float boxWidth = 170f;
    [SerializeField] float boxHeight = 46f;
    [SerializeField] float rowHeight = 56f;

    static readonly Color colBox = new Color(0.16f, 0.2f, 0.28f, 0.95f);
    static readonly Color colDone = new Color(0.12f, 0.14f, 0.18f, 0.95f);
    static readonly Color colPlayer = new Color(0.2f, 0.42f, 0.75f, 0.95f);
    static readonly Color colNext = new Color(0.95f, 0.75f, 0.2f, 1f);
    static readonly Color colWinner = new Color(1f, 0.9f, 0.35f);

    BracketMatch nextMatch;
    Vector2 scroll;
    GUIStyle title, header, team, teamBold, small, bigButton;

    void Start()
    {
        if (TournamentSession.IsActive && TournamentSession.Config == config) Advance();
    }

    void Advance()
    {
        nextMatch = TournamentSession.AdvanceToPlayerMatch();
    }

    void NewTournament()
    {
        TournamentSession.ClearSave();
        if (TournamentSession.StartNew(config)) Advance();
    }

    // ---------- Dibujo ----------

    void OnGUI()
    {
        EnsureStyles();
        GUI.color = new Color(0.07f, 0.09f, 0.13f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (config == null)
        {
            GUI.Label(new Rect(20, 20, 800, 40), "Asigna un TournamentConfig al TournamentHub", header);
            return;
        }

        if (!TournamentSession.IsActive || TournamentSession.Config != config) { DrawStartMenu(); return; }

        DoubleEliminationBracket b = TournamentSession.Bracket;
        GUI.Label(new Rect(20, 12, Screen.width - 40, 50), config.TournamentName, title);

        float sideW = 300f;
        Rect view = new Rect(20, 70, Screen.width - sideW - 60, Screen.height - 190);
        DrawBracket(b, view);
        DrawSidePanel(new Rect(view.xMax + 20, 70, sideW, view.height));
        DrawBottomBar(b, new Rect(20, Screen.height - 105, Screen.width - 40, 90));
    }

    void DrawStartMenu()
    {
        float cx = Screen.width * 0.5f;
        GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 70), config.TournamentName, title);
        GUI.Label(new Rect(0, Screen.height * 0.25f + 70, Screen.width, 40),
                  "Doble eliminación · " + config.Teams.Count + " equipos · Dos derrotas y fuera", header);

        string error;
        if (!config.IsValid(out error))
        {
            GUI.Label(new Rect(0, Screen.height * 0.5f, Screen.width, 40), error, header);
            return;
        }

        float y = Screen.height * 0.5f;
        if (TournamentSession.HasSave(config))
        {
            if (GUI.Button(new Rect(cx - 160, y, 320, 70), "CONTINUAR", bigButton) &&
                TournamentSession.TryResume(config)) Advance();
            y += 90;
        }
        if (GUI.Button(new Rect(cx - 160, y, 320, 70), "NUEVO TORNEO", bigButton)) NewTournament();
    }

    void DrawBracket(DoubleEliminationBracket b, Rect view)
    {
        int wR = b.winnersRounds, lR = b.losersRounds;
        float headerH = 30f;
        float winnersH = (b.teamCount / 2) * rowHeight;
        float losersH = Mathf.Max(1, b.teamCount / 4) * rowHeight;
        float losersTop = headerH + winnersH + 40f + headerH;
        int cols = Mathf.Max(wR, lR) + 1;

        Rect content = new Rect(0, 0, cols * columnWidth + 20f, losersTop + losersH + 10f);
        scroll = GUI.BeginScrollView(view, scroll, content);

        GUI.Label(new Rect(0, 0, 400, headerH), "CUADRO DE GANADORES", header);
        for (int r = 0; r < wR; r++)
            DrawColumn(b, b.Round(BracketSide.Winners, r), r * columnWidth, headerH, winnersH);

        GUI.Label(new Rect(0, losersTop - headerH, 400, headerH), "CUADRO DE PERDEDORES", header);
        for (int r = 0; r < lR; r++)
            DrawColumn(b, b.Round(BracketSide.Losers, r), r * columnWidth, losersTop, losersH);

        // Gran final (y desempate si lo hay) a la derecha, entre los dos cuadros
        float gx = (cols - 1) * columnWidth;
        float gy = headerH + winnersH + 20f - boxHeight;
        GUI.Label(new Rect(gx, gy - headerH, columnWidth, headerH), "GRAN FINAL", header);
        DrawMatch(b, b.Get(b.grandFinalId), new Rect(gx, gy, boxWidth, boxHeight));
        if (b.resetMatchId >= 0)
            DrawMatch(b, b.Get(b.resetMatchId), new Rect(gx, gy + boxHeight + 10f, boxWidth, boxHeight));

        GUI.EndScrollView();
    }

    void DrawColumn(DoubleEliminationBracket b, List<BracketMatch> round, float x, float top, float height)
    {
        float slot = height / round.Count;
        for (int i = 0; i < round.Count; i++)
        {
            float y = top + (i + 0.5f) * slot - boxHeight * 0.5f;
            DrawMatch(b, round[i], new Rect(x, y, boxWidth, boxHeight));
        }
    }

    void DrawMatch(DoubleEliminationBracket b, BracketMatch m, Rect r)
    {
        int player = TournamentSession.PlayerTeam;
        bool isNext = nextMatch != null && m.id == nextMatch.id;

        if (isNext)
        {
            GUI.color = colNext;
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
        }

        GUI.color = m.Involves(player) ? colPlayer : (m.IsDone ? colDone : colBox);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float h = r.height * 0.5f;
        DrawTeamLine(m, m.teamA, m.scoreA, new Rect(r.x + 6, r.y, r.width - 12, h));
        DrawTeamLine(m, m.teamB, m.scoreB, new Rect(r.x + 6, r.y + h, r.width - 12, h));
    }

    void DrawTeamLine(BracketMatch m, int teamIndex, int score, Rect r)
    {
        bool won = m.IsDone && m.winner == teamIndex;
        GUIStyle s = won ? teamBold : team;
        string name = teamIndex >= 0 ? TournamentSession.Team(teamIndex).ShortName : "—";

        GUI.Label(new Rect(r.x, r.y, r.width - 30, r.height), name, s);
        if (m.IsDone) GUI.Label(new Rect(r.xMax - 30, r.y, 30, r.height), score.ToString(), s);
    }

    void DrawSidePanel(Rect r)
    {
        GUI.color = new Color(1, 1, 1, 0.06f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20));
        GUILayout.Label("RESULTADOS", header);

        if (TournamentSession.LastSimulated.Count == 0) GUILayout.Label("Nada nuevo desde tu último partido", small);
        foreach (BracketMatch m in TournamentSession.LastSimulated)
        {
            TeamData a = TournamentSession.Team(m.teamA), c = TournamentSession.Team(m.teamB);
            GUILayout.Label(TournamentSession.Bracket.RoundName(m), small);
            GUILayout.Label(a.ShortName + "  " + m.scoreA + " - " + m.scoreB + "  " + c.ShortName, team);
            GUILayout.Space(4);
        }

        GUILayout.Space(10);
        int losses = TournamentSession.Bracket.Losses(TournamentSession.PlayerTeam);
        GUILayout.Label("Tus derrotas: " + losses + " / 2", header);
        GUILayout.EndArea();
    }

    void DrawBottomBar(DoubleEliminationBracket b, Rect r)
    {
        GUI.color = new Color(1, 1, 1, 0.08f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect text = new Rect(r.x + 20, r.y, r.width - 400, r.height);
        Rect button = new Rect(r.xMax - 340, r.y + 12, 320, r.height - 24);

        if (b.IsFinished)
        {
            TeamData champ = TournamentSession.Team(b.championTeam);
            string line = TournamentSession.IsChampion ? "¡SOIS CAMPEONES DE LA " + config.TournamentName.ToUpper() + "!"
                                                       : "Campeón: " + champ.TeamName;
            GUI.Label(text, line, title);
            if (GUI.Button(button, "NUEVO TORNEO", bigButton)) NewTournament();
            return;
        }

        if (nextMatch == null) return;

        TeamData rival = TournamentSession.RivalIn(nextMatch);
        GUI.Label(new Rect(text.x, text.y + 8, text.width, 30), b.RoundName(nextMatch).ToUpper(), small);
        GUI.Label(new Rect(text.x, text.y + 32, text.width, 50),
                  TournamentSession.Team(TournamentSession.PlayerTeam).TeamName + "  vs  " + rival.TeamName, header);

        if (GUI.Button(button, "JUGAR", bigButton)) SceneManager.LoadScene(config.MatchScene);
    }

    void EnsureStyles()
    {
        if (title != null) return;

        title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        header = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        team = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
        teamBold = new GUIStyle(team) { fontStyle = FontStyle.Bold };
        teamBold.normal.textColor = colWinner;
        small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        small.normal.textColor = new Color(0.7f, 0.75f, 0.85f);
        bigButton = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
    }
}
