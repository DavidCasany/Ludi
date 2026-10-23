using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Escena "hub" del torneo: menú de inicio, cuadro de doble eliminación, resultados simulados,
// elección de mejoras (roguelite) y botón para jugar el siguiente partido.
// Dibujado con OnGUI como el resto del HUD provisional; cuando tengáis el arte, se cambia por
// UI Toolkit/Canvas leyendo los mismos datos de TournamentSession.
public class TournamentHub : MonoBehaviour
{
    [SerializeField] TournamentConfig config;

    [Header("Cuadro")]
    [SerializeField] float columnWidth = 190f;
    [SerializeField] float boxWidth = 170f;
    [SerializeField] float boxHeight = 46f;
    [SerializeField] float rowHeight = 56f;

    [Header("Cartas de mejora")]
    [SerializeField] float cardWidth = 280f;
    [SerializeField] float cardHeight = 340f;

    static readonly Color colBox = new Color(0.16f, 0.2f, 0.28f, 0.95f);
    static readonly Color colDone = new Color(0.12f, 0.14f, 0.18f, 0.95f);
    static readonly Color colPlayer = new Color(0.2f, 0.42f, 0.75f, 0.95f);
    static readonly Color colNext = new Color(0.95f, 0.75f, 0.2f, 1f);
    static readonly Color colWinner = new Color(1f, 0.9f, 0.35f);
    static readonly Color colCard = new Color(0.13f, 0.16f, 0.22f, 1f);

    BracketMatch nextMatch;
    Vector2 scroll;
    GUIStyle title, header, team, teamBold, small, bigButton, cardTitle, cardText, cardFlavor;
    GUIStyle cardButton, headerCenter, tooltipStyle;

    void Start()
    {
        if (TournamentSession.IsActive && TournamentSession.Config == config) Advance();
    }

    void Advance()
    {
        nextMatch = TournamentSession.AdvanceToPlayerMatch();
        TournamentSession.BeginPickRound();
    }

    void NewTournament()
    {
        TournamentSession.ClearSave();
        if (TournamentSession.StartNew(config)) Advance();
    }

    bool Drafting => nextMatch != null && TournamentSession.PlayerPicksPending > 0;

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

        // Mientras se eligen mejoras, el cuadro queda detrás y no se puede tocar
        GUI.enabled = !Drafting;
        DrawBracket(b, view);
        DrawSidePanel(new Rect(view.xMax + 20, 70, sideW, view.height));
        DrawBottomBar(b, new Rect(20, Screen.height - 105, Screen.width - 40, 90));
        GUI.enabled = true;

        if (Drafting) DrawDraft();
        else DrawTooltip();
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

    // ---------- Cuadro ----------

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
            DrawColumn(b.Round(BracketSide.Winners, r), r * columnWidth, headerH, winnersH);

        GUI.Label(new Rect(0, losersTop - headerH, 400, headerH), "CUADRO DE PERDEDORES", header);
        for (int r = 0; r < lR; r++)
            DrawColumn(b.Round(BracketSide.Losers, r), r * columnWidth, losersTop, losersH);

        // Gran final (y desempate si lo hay) a la derecha, entre los dos cuadros
        float gx = (cols - 1) * columnWidth;
        float gy = headerH + winnersH + 20f - boxHeight;
        GUI.Label(new Rect(gx, gy - headerH, columnWidth, headerH), "GRAN FINAL", header);
        DrawMatch(b.Get(b.grandFinalId), new Rect(gx, gy, boxWidth, boxHeight));
        if (b.resetMatchId >= 0)
            DrawMatch(b.Get(b.resetMatchId), new Rect(gx, gy + boxHeight + 10f, boxWidth, boxHeight));

        GUI.EndScrollView();
    }

    void DrawColumn(List<BracketMatch> round, float x, float top, float height)
    {
        float slot = height / round.Count;
        for (int i = 0; i < round.Count; i++)
        {
            float y = top + (i + 0.5f) * slot - boxHeight * 0.5f;
            DrawMatch(round[i], new Rect(x, y, boxWidth, boxHeight));
        }
    }

    void DrawMatch(BracketMatch m, Rect r)
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

    // ---------- Panel lateral ----------

    void DrawSidePanel(Rect r)
    {
        GUI.color = new Color(1, 1, 1, 0.06f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 20));

        int losses = TournamentSession.Bracket.Losses(TournamentSession.PlayerTeam);
        GUILayout.Label("Tus derrotas: " + losses + " / 2", header);
        GUILayout.Space(8);

        if (TournamentSession.UpgradesEnabled)
        {
            GUILayout.Label("TUS MEJORAS", header);
            DrawOwnedUpgrades(TournamentSession.PlayerTeam);
            GUILayout.Space(10);
        }

        GUILayout.Label("RESULTADOS", header);
        if (TournamentSession.LastSimulated.Count == 0) GUILayout.Label("Nada nuevo desde tu último partido", small);
        foreach (BracketMatch m in TournamentSession.LastSimulated)
        {
            TeamData a = TournamentSession.Team(m.teamA), c = TournamentSession.Team(m.teamB);
            GUILayout.Label(TournamentSession.Bracket.RoundName(m), small);
            GUILayout.Label(a.ShortName + "  " + m.scoreA + " - " + m.scoreB + "  " + c.ShortName, team);
        }

        GUILayout.EndArea();
    }

    // Lista agrupada: "Patines afilados x2"
    void DrawOwnedUpgrades(int teamIndex)
    {
        IReadOnlyList<int> ids = TournamentSession.UpgradeIdsOf(teamIndex);
        if (ids.Count == 0) { GUILayout.Label("Ninguna todavía", small); return; }

        Dictionary<int, int> counts = new Dictionary<int, int>();
        List<int> order = new List<int>();
        foreach (int id in ids)
        {
            if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); }
            counts[id]++;
        }

        foreach (int id in order)
        {
            UpgradeData u = TournamentSession.Upgrade(id);
            if (u == null) continue;
            string label = u.DisplayName + (counts[id] > 1 ? "  x" + counts[id] : "");
            GUILayout.Label(new GUIContent(label, ColoredEffects(u)), team);   // los efectos salen al pasar el ratón
        }
    }

    // ---------- Barra inferior ----------

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

        int rivalIndex = nextMatch.OpponentOf(TournamentSession.PlayerTeam);
        TeamData rival = TournamentSession.Team(rivalIndex);
        string rivalInfo = TournamentSession.UpgradesEnabled
            ? "   ·   El rival lleva " + TournamentSession.UpgradeIdsOf(rivalIndex).Count + " mejoras"
            : "";

        GUI.Label(new Rect(text.x, text.y + 8, text.width, 30), b.RoundName(nextMatch).ToUpper() + rivalInfo, small);
        GUI.Label(new Rect(text.x, text.y + 32, text.width, 50),
                  TournamentSession.Team(TournamentSession.PlayerTeam).TeamName + "  vs  " + rival.TeamName, header);

        if (GUI.Button(button, "JUGAR", bigButton)) SceneManager.LoadScene(config.MatchScene);
    }

    // ---------- Elección de mejora ----------

    void DrawDraft()
    {
        IReadOnlyList<int> offer = TournamentSession.PlayerOffer;
        if (offer.Count == 0) return;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        int pending = TournamentSession.PlayerPicksPending;
        int total = Mathf.Max(pending, TournamentSession.PlayerPicksThisRound);
        int current = total - pending + 1;

        float top = Screen.height * 0.5f - cardHeight * 0.5f;
        GUI.Label(new Rect(0, top - 110, Screen.width, 50), "ELIGE UNA MEJORA", title);

        string sub = total > 1
            ? $"Mejora {current} de {total}  ·  Has avanzado sin esperar: recuperas las jornadas que te has saltado"
            : "Todos los equipos eligen una mejora por jornada";
        GUI.Label(new Rect(0, top - 60, Screen.width, 30), sub, headerCenter);

        float gap = 30f;
        float totalW = offer.Count * cardWidth + (offer.Count - 1) * gap;
        float x0 = (Screen.width - totalW) * 0.5f;

        for (int i = 0; i < offer.Count; i++)
        {
            Rect card = new Rect(x0 + i * (cardWidth + gap), top, cardWidth, cardHeight);
            if (DrawCard(TournamentSession.Upgrade(offer[i]), card, i + 1))
            {
                TournamentSession.ChoosePlayerUpgrade(i);
                return;
            }
        }

        // Atajos de teclado 1, 2, 3
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            int k = e.keyCode - KeyCode.Alpha1;
            if (k >= 0 && k < offer.Count)
            {
                TournamentSession.ChoosePlayerUpgrade(k);
                e.Use();
            }
        }
    }

    // Devuelve true si se ha pulsado
    bool DrawCard(UpgradeData u, Rect r, int number)
    {
        if (u == null) return false;
        Color rc = RarityColor(u.Rarity);

        GUI.color = rc;
        GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture);
        GUI.color = colCard;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        float pad = 16f, y = r.y + pad, w = r.width - pad * 2;

        GUIStyle rarity = new GUIStyle(small) { fontStyle = FontStyle.Bold };
        rarity.normal.textColor = rc;
        GUI.Label(new Rect(r.x + pad, y, w, 20), UpgradeData.RarityName(u.Rarity) + "   [" + number + "]", rarity);
        y += 24;

        if (u.Icon != null)
        {
            GUI.DrawTexture(new Rect(r.center.x - 32, y, 64, 64), u.Icon.texture, ScaleMode.ScaleToFit);
            y += 70;
        }

        GUI.Label(new Rect(r.x + pad, y, w, 60), u.DisplayName, cardTitle);
        y += 64;

        GUI.Label(new Rect(r.x + pad, y, w, 120), ColoredEffects(u), cardText);
        y += 120;

        if (!string.IsNullOrEmpty(u.FlavorText))
            GUI.Label(new Rect(r.x + pad, y, w, r.yMax - y - 60), u.FlavorText, cardFlavor);

        return GUI.Button(new Rect(r.x + pad, r.yMax - 52, w, 40), "ELEGIR", cardButton);
    }

    static string ColoredEffects(UpgradeData u)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < u.Modifiers.Count; i++)
        {
            StatModifier m = u.Modifiers[i];
            if (i > 0) sb.Append('\n');
            sb.Append(m.IsBeneficial ? "<color=#7CFF8A>" : "<color=#FF7C7C>").Append(m.Describe()).Append("</color>");
        }
        return sb.ToString();
    }

    static Color RarityColor(UpgradeRarity r)
    {
        switch (r)
        {
            case UpgradeRarity.Epica: return new Color(0.75f, 0.45f, 1f);
            case UpgradeRarity.Rara: return new Color(0.35f, 0.65f, 1f);
            default: return new Color(0.65f, 0.7f, 0.78f);
        }
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

        cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
        cardText = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true };
        cardFlavor = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Italic, wordWrap = true };
        cardFlavor.normal.textColor = new Color(0.65f, 0.7f, 0.8f);
        cardButton = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
        headerCenter = new GUIStyle(header) { alignment = TextAnchor.MiddleCenter };
        tooltipStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, richText = true, alignment = TextAnchor.UpperLeft, wordWrap = true };
    }

    // Recuadro con los efectos al pasar el ratón por una mejora de la lista
    void DrawTooltip()
    {
        if (string.IsNullOrEmpty(GUI.tooltip)) return;
        Vector2 m = Event.current.mousePosition;
        float h = tooltipStyle.CalcHeight(new GUIContent(GUI.tooltip), 240f) + 8f;
        Rect r = new Rect(Mathf.Min(m.x + 16f, Screen.width - 250f), m.y + 16f, 240f, h);
        GUI.Box(r, GUI.tooltip, tooltipStyle);
    }
}
