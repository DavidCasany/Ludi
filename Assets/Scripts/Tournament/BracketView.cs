using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Dibuja el cuadro de doble eliminación dentro de un RectTransform (el 'content' de un ScrollRect).
//
//   CUADRO DE GANADORES   columnas por ronda ──┐
//                                              ├── GRAN FINAL (+ desempate)
//   CUADRO DE PERDEDORES  columnas por ronda ──┘
//
// Cada partido es una caja con los dos equipos y el marcador. Las líneas unen cada partido con el
// partido al que va su ganador; el camino que ha hecho el jugador se pinta en su color.
// No guarda estado: se vuelve a construir entero cada vez que cambia el torneo.
public static class BracketView
{
    const float ColW = 256f;        // ancho de columna (caja + hueco para las líneas)
    const float BoxW = 214f;
    const float BoxH = 70f;
    const float RowH = 84f;         // alto por partido en la primera ronda
    const float SectionTitleH = 34f;
    const float ColumnTitleH = 26f;
    const float SectionGap = 56f;
    const float LineWidth = 2f;

    public struct Result
    {
        public Vector2 size;          // tamaño total del contenido
        public Vector2 focus;         // centro de la caja del próximo partido (o (0,0) si no hay)
        public bool hasFocus;
    }

    public static Result Build(RectTransform content, DoubleEliminationBracket b, int player, BracketMatch next)
    {
        int n = b.teamCount;
        int wR = b.winnersRounds, lR = b.losersRounds;
        int cols = Mathf.Max(wR, lR) + 1;

        float headerH = SectionTitleH + ColumnTitleH;
        float winnersTop = headerH;
        float winnersH = (n / 2) * RowH;
        float losersHeader = winnersTop + winnersH + SectionGap;
        float losersTop = losersHeader + headerH;
        float losersH = Mathf.Max(1, n / 4) * RowH;

        Result result = new Result();
        result.size = new Vector2(cols * ColW, losersTop + losersH + 16f);

        RectTransform lines = UIKit.Rect(content, "Lines");
        UIKit.Stretch(lines, 0f);
        RectTransform boxes = UIKit.Rect(content, "Matches");
        UIKit.Stretch(boxes, 0f);

        // ---------- Títulos ----------
        SectionTitle(content, "CUADRO DE GANADORES", 0f);
        SectionTitle(content, "CUADRO DE PERDEDORES", losersHeader);
        for (int r = 0; r < wR; r++) ColumnTitle(content, ColumnName(BracketSide.Winners, r, wR, lR), r, SectionTitleH);
        for (int r = 0; r < lR; r++) ColumnTitle(content, ColumnName(BracketSide.Losers, r, wR, lR), r, losersHeader + SectionTitleH);

        // ---------- Posiciones (esquina superior izquierda de cada caja) ----------
        Dictionary<int, Vector2> pos = new Dictionary<int, Vector2>();
        for (int r = 0; r < wR; r++) LayoutColumn(b.Round(BracketSide.Winners, r), r, winnersTop, winnersH, pos);
        for (int r = 0; r < lR; r++) LayoutColumn(b.Round(BracketSide.Losers, r), r, losersTop, losersH, pos);

        // Gran final: última columna, a media altura entre la final de ganadores y la de perdedores
        float gfX = (cols - 1) * ColW;
        float gfCenter = (winnersTop + winnersH * 0.5f + losersTop + losersH * 0.5f) * 0.5f;
        pos[b.grandFinalId] = new Vector2(gfX, gfCenter - BoxH * 0.5f);
        ColumnTitle(content, "GRAN FINAL", cols - 1, gfCenter - BoxH * 0.5f - ColumnTitleH - 6f, UIKit.Gold);
        if (b.resetMatchId >= 0)
        {
            pos[b.resetMatchId] = new Vector2(gfX, gfCenter + BoxH * 0.5f + ColumnTitleH + 18f);
            ColumnTitle(content, "DESEMPATE", cols - 1, gfCenter + BoxH * 0.5f + 14f, UIKit.Gold);
        }

        // ---------- Líneas (detrás de las cajas) ----------
        foreach (BracketMatch m in b.matches)
        {
            if (m.side != BracketSide.Winners && m.side != BracketSide.Losers) continue;
            if (m.winnerToMatch < 0 || !pos.ContainsKey(m.id) || !pos.ContainsKey(m.winnerToMatch)) continue;

            Vector2 from = pos[m.id] + new Vector2(BoxW, BoxH * 0.5f);
            Vector2 to = pos[m.winnerToMatch] + new Vector2(0f, BoxH * 0.5f);
            bool playerPath = player >= 0 && m.winner == player;
            Color c = playerPath ? UIKit.Player : (m.IsDone ? UIKit.Alpha(Color.white, 0.22f) : UIKit.Line);
            Elbow(lines, from, to, c);
        }

        // ---------- Cajas ----------
        foreach (BracketMatch m in b.matches)
        {
            if (!pos.ContainsKey(m.id)) continue;
            bool isNext = next != null && m.id == next.id;
            MatchBox(boxes, b, m, pos[m.id], player, isNext);
            if (isNext)
            {
                result.focus = pos[m.id] + new Vector2(BoxW, BoxH) * 0.5f;
                result.hasFocus = true;
            }
        }
        return result;
    }

    // ---------- Colocación ----------

    static void LayoutColumn(List<BracketMatch> round, int col, float top, float height, Dictionary<int, Vector2> pos)
    {
        if (round.Count == 0) return;
        float slot = height / round.Count;
        for (int i = 0; i < round.Count; i++)
        {
            float cy = top + (i + 0.5f) * slot;
            pos[round[i].id] = new Vector2(col * ColW, cy - BoxH * 0.5f);
        }
    }

    static string ColumnName(BracketSide side, int r, int wR, int lR)
    {
        if (side == BracketSide.Winners)
        {
            int fromEnd = wR - 1 - r;
            if (fromEnd == 0) return "FINAL";
            if (fromEnd == 1) return "SEMIFINALES";
            if (fromEnd == 2) return "CUARTOS";
            return "RONDA " + (r + 1);
        }
        return r == lR - 1 ? "FINAL" : "RONDA " + (r + 1);
    }

    static void SectionTitle(RectTransform parent, string text, float y)
    {
        TextMeshProUGUI t = UIKit.Text(parent, text, text, 20f, UIKit.Accent, FontStyles.Bold, TextAlignmentOptions.Left);
        t.characterSpacing = 8f;
        UIKit.PlaceTopLeft(t.rectTransform, 0f, y, 600f, SectionTitleH);
    }

    static void ColumnTitle(RectTransform parent, string text, int col, float y, Color? color = null)
    {
        TextMeshProUGUI t = UIKit.Text(parent, "Col_" + text, text, 15f, color ?? UIKit.TextFaint, FontStyles.Bold,
                                       TextAlignmentOptions.Left);
        t.characterSpacing = 6f;
        UIKit.PlaceTopLeft(t.rectTransform, col * ColW + 4f, y, BoxW, ColumnTitleH);
    }

    // ---------- Líneas ----------

    // De la derecha de una caja a la izquierda de otra: horizontal - vertical - horizontal
    static void Elbow(RectTransform parent, Vector2 from, Vector2 to, Color c)
    {
        float midX = from.x + (to.x - from.x) * 0.5f;
        HLine(parent, from.x, midX, from.y, c);
        if (!Mathf.Approximately(from.y, to.y)) VLine(parent, midX, from.y, to.y, c);
        HLine(parent, midX, to.x, to.y, c);
    }

    static void HLine(RectTransform parent, float x1, float x2, float y, Color c)
    {
        Image img = UIKit.Image(parent, "H", c);
        UIKit.PlaceTopLeft(img.rectTransform, Mathf.Min(x1, x2), y - LineWidth * 0.5f,
                           Mathf.Abs(x2 - x1) + LineWidth * 0.5f, LineWidth);
    }

    static void VLine(RectTransform parent, float x, float y1, float y2, Color c)
    {
        Image img = UIKit.Image(parent, "V", c);
        UIKit.PlaceTopLeft(img.rectTransform, x - LineWidth * 0.5f, Mathf.Min(y1, y2),
                           LineWidth, Mathf.Abs(y2 - y1) + LineWidth * 0.5f);
    }

    // ---------- Caja de un partido ----------

    static void MatchBox(RectTransform parent, DoubleEliminationBracket b, BracketMatch m, Vector2 topLeft,
                         int player, bool isNext)
    {
        bool mine = m.Involves(player);

        RectTransform holder = UIKit.Rect(parent, "Match_" + m.id);
        UIKit.PlaceTopLeft(holder, topLeft.x, topLeft.y, BoxW, BoxH);
        holder.pivot = new Vector2(0.5f, 0.5f);                       // el "pop" del ratón desde el centro
        holder.anchoredPosition += new Vector2(BoxW * 0.5f, -BoxH * 0.5f);

        // Marco: naranja = tu próximo partido, azul = partido tuyo
        if (isNext || mine)
        {
            Image frame = UIKit.Image(holder, "Frame", isNext ? UIKit.Accent : UIKit.Alpha(UIKit.Player, 0.8f), 12f);
            UIKit.Stretch(frame.rectTransform, isNext ? -3f : -2f);
        }

        Color bodyColor = m.IsDone ? UIKit.Panel : (m.IsReady ? UIKit.Surface : UIKit.Alpha(UIKit.Surface, 0.55f));
        Image body = UIKit.Image(holder, "Body", bodyColor, 10f, true);
        UIKit.Stretch(body.rectTransform, 0f);
        holder.gameObject.AddComponent<HoverPop>().Scale = 1.04f;

        Image divider = UIKit.Image(holder, "Divider", UIKit.Alpha(Color.white, 0.06f));
        UIKit.Place(divider.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BoxW - 16f, 1f));

        TeamRow(holder, m, m.teamA, m.scoreA, 0, player);
        TeamRow(holder, m, m.teamB, m.scoreB, 1, player);

        if (isNext)
        {
            Image tag = UIKit.Image(holder, "NextTag", UIKit.Accent, 8f);
            UIKit.Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(8f, 12f), new Vector2(92f, 22f));
            TextMeshProUGUI t = UIKit.Text(tag.transform, "Text", "PRÓXIMO", 13f, Color.white, FontStyles.Bold);
            t.characterSpacing = 4f;
            UIKit.Stretch(t.rectTransform, 0f);
        }
    }

    static void TeamRow(RectTransform box, BracketMatch m, int team, int score, int row, int player)
    {
        float h = BoxH * 0.5f;
        float y = -row * h;   // desde arriba

        bool known = team >= 0;
        bool won = m.IsDone && m.winner == team;
        bool lost = m.IsDone && m.loser == team;
        bool isPlayer = known && team == player;
        TeamData data = known ? TournamentSession.Team(team) : null;

        if (known)
        {
            Image chip = UIKit.Image(box, "Chip", data.PrimaryColor, 3f);
            UIKit.PlaceTopLeft(chip.rectTransform, 10f, -y + h * 0.5f - 9f, 6f, 18f);
        }

        Color nameColor = !known ? UIKit.TextFaint : lost ? UIKit.TextFaint : isPlayer ? new Color(0.6f, 0.8f, 1f) : UIKit.TextMain;
        string label = known ? data.TeamName : "Por decidir";
        TextMeshProUGUI name = UIKit.Text(box, "Team", label, 19f, nameColor,
                                          won || isPlayer ? FontStyles.Bold : (known ? FontStyles.Normal : FontStyles.Italic),
                                          TextAlignmentOptions.Left);
        UIKit.PlaceTopLeft(name.rectTransform, 24f, -y, BoxW - 24f - 44f, h);

        if (m.IsDone)
        {
            TextMeshProUGUI s = UIKit.Text(box, "Score", score.ToString(), 21f, won ? UIKit.Gold : UIKit.TextFaint,
                                           FontStyles.Bold, TextAlignmentOptions.Right);
            UIKit.PlaceTopLeft(s.rectTransform, BoxW - 44f, -y, 32f, h);
        }
    }
}
