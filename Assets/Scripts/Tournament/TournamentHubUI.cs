using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Vista de la escena del torneo (el "hub"). Solo dibuja: las decisiones las toma TournamentHub.
//
//  ┌───────────────────────── MENÚ · Copa Ludi · jornada ─────────────────────────┐
//  │  CUADRO (ScrollRect con BracketView)                  │  TU EQUIPO            │
//  │                                                       │  vidas · mejoras      │
//  │                                                       │  últimos resultados   │
//  ├───────────────────────────────────────────────────────┴───────────────────────┤
//  │  PRÓXIMO PARTIDO  ·  ronda      [escudo] Tú  VS  Rival [escudo]       JUGAR    │
//  └───────────────────────────────────────────────────────────────────────────────┘
// Encima, cuando toca: elección de mejoras (red de seguridad), torneo terminado o "no hay torneo".
public class TournamentHubUI : MonoBehaviour
{
    const float Margin = 40f;
    const float TopBarH = 120f;
    const float BottomBarH = 160f;
    const float SideW = 480f;
    const float Gap = 24f;

    public event Action PlayClicked, MenuClicked, NewTournamentClicked;

    RectTransform root, screen, overlay;
    ScrollRect scroll;
    Button playButton;

    public bool HasOverlay => overlay != null;
    public bool CanPlay => playButton != null && playButton.interactable && overlay == null;

    public static TournamentHubUI Create()
    {
        Canvas canvas = UIKit.CreateCanvas("TournamentHubUI", 0);
        TournamentHubUI ui = canvas.gameObject.AddComponent<TournamentHubUI>();
        ui.root = (RectTransform)canvas.transform;
        Image bg = UIKit.Image(ui.root, "Background", UIKit.Background, 0f, true);
        UIKit.Stretch(bg.rectTransform, 0f);
        return ui;
    }

    // ---------- Pantalla principal ----------

    public void Render(TournamentConfig config, BracketMatch next)
    {
        if (screen != null) Destroy(screen.gameObject);
        screen = UIKit.Rect(root, "Screen");
        UIKit.Stretch(screen, 0f);
        screen.SetSiblingIndex(1);   // detrás de cualquier capa (overlay) que ya esté abierta

        DoubleEliminationBracket b = TournamentSession.Bracket;
        int player = TournamentSession.PlayerTeam;

        BuildTopBar(config, next);

        // Cuadro
        Image panel = UIKit.Image(screen, "BracketPanel", UIKit.Panel, 18f);
        UIKit.Stretch(panel.rectTransform, Margin, Margin + BottomBarH + Gap, Margin + SideW + Gap, TopBarH + 10f);
        RectTransform content = BuildScroll(panel.rectTransform);
        BracketView.Result r = BracketView.Build(content, b, player, next);
        content.sizeDelta = r.size;
        if (r.hasFocus) FocusOn(r.focus, r.size);

        BuildSidePanel(player);
        BuildBottomBar(config, next);
    }

    void BuildTopBar(TournamentConfig config, BracketMatch next)
    {
        Button menu = UIKit.Button(screen, "Menu", "MENÚ", false, 24f);
        UIKit.Place((RectTransform)menu.transform, new Vector2(0f, 1f), new Vector2(Margin, -34f), new Vector2(180f, 58f));
        menu.onClick.AddListener(() => MenuClicked?.Invoke());

        TextMeshProUGUI title = UIKit.Text(screen, "Title", config.TournamentName.ToUpper(), 50f, UIKit.TextMain, FontStyles.Bold);
        title.characterSpacing = 8f;
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1000f, 62f));

        TextMeshProUGUI sub = UIKit.Text(screen, "Subtitle",
            "DOBLE ELIMINACIÓN  ·  " + config.Teams.Count + " EQUIPOS  ·  DOS DERROTAS Y FUERA",
            17f, UIKit.Accent, FontStyles.Bold);
        sub.characterSpacing = 6f;
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(1000f, 26f));

        if (next != null)
        {
            TextMeshProUGUI day = UIKit.Text(screen, "Matchday", "JORNADA " + (next.Stage + 1), 22f, UIKit.TextSoft,
                                             FontStyles.Bold, TextAlignmentOptions.Right);
            day.characterSpacing = 6f;
            UIKit.Place(day.rectTransform, new Vector2(1f, 1f), new Vector2(-Margin, -46f), new Vector2(300f, 34f));
        }
    }

    RectTransform BuildScroll(RectTransform panel)
    {
        RectTransform viewport = UIKit.Rect(panel, "Viewport");
        UIKit.Stretch(viewport, 24f);
        viewport.gameObject.AddComponent<RectMask2D>();
        Image hit = viewport.gameObject.AddComponent<Image>();   // para poder arrastrar por los huecos
        hit.color = Color.clear;

        RectTransform content = UIKit.Rect(viewport, "Content");
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;

        scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        return content;
    }

    // Deja el próximo partido a la vista si el cuadro no cabe entero (16 equipos)
    void FocusOn(Vector2 focus, Vector2 size)
    {
        Canvas.ForceUpdateCanvases();
        Vector2 view = scroll.viewport.rect.size;
        if (size.x > view.x) scroll.horizontalNormalizedPosition = Mathf.Clamp01((focus.x - view.x * 0.5f) / (size.x - view.x));
        if (size.y > view.y) scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((focus.y - view.y * 0.5f) / (size.y - view.y));
    }

    // ---------- Panel lateral: tu equipo ----------

    void BuildSidePanel(int player)
    {
        Image panel = UIKit.Image(screen, "SidePanel", UIKit.Panel, 18f);
        RectTransform rt = panel.rectTransform;
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.offsetMin = new Vector2(-Margin - SideW, Margin + BottomBarH + Gap);
        rt.offsetMax = new Vector2(-Margin, -TopBarH - 10f);

        VerticalLayoutGroup v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(28, 28, 24, 24);
        v.spacing = 10f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        DoubleEliminationBracket b = TournamentSession.Bracket;
        TeamData team = TournamentSession.Team(player);

        Label(panel.transform, "TU EQUIPO", UIKit.Accent);

        // Escudo + nombre + balance
        RectTransform teamRow = Row(panel.transform, 84f, 18f);
        RectTransform badge = UIKit.TeamBadge(teamRow, team, 76f);
        UIKit.Size(badge, 76f, 76f);
        RectTransform names = UIKit.Rect(teamRow, "Names");
        UIKit.Size(names, -1f, 76f, 1f);
        TextMeshProUGUI teamName = UIKit.Text(names, "Name", team.TeamName, 32f, UIKit.TextMain, FontStyles.Bold,
                                              TextAlignmentOptions.BottomLeft);
        UIKit.Stretch(teamName.rectTransform, 0f, 34f, 0f, 0f);
        int wins = 0, losses = b.Losses(player);
        foreach (BracketMatch m in b.matches) if (m.winner == player) wins++;
        TextMeshProUGUI record = UIKit.Text(names, "Record", wins + " victorias  ·  " + losses + " derrotas", 20f,
                                            UIKit.TextSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        UIKit.Stretch(record.rectTransform, 0f, 0f, 0f, 46f);

        // Vidas: dos derrotas y fuera
        RectTransform lives = Row(panel.transform, 30f, 10f);
        TextMeshProUGUI livesLabel = UIKit.Text(lives, "Label", "VIDAS", 17f, UIKit.TextSoft, FontStyles.Bold,
                                                TextAlignmentOptions.Left);
        livesLabel.characterSpacing = 6f;
        UIKit.Size(livesLabel, 80f, 30f);
        for (int i = 0; i < 2; i++)
        {
            bool alive = i < 2 - losses;
            Image pip = UIKit.CircleImage(lives, "Life", alive ? UIKit.Good : UIKit.Alpha(Color.white, 0.12f), 22f);
            UIKit.Size(pip, 22f, 22f);
        }
        string livesText = TournamentSession.PlayerEliminated ? "Eliminado"
                         : losses == 0 ? "Invicto" : "Una derrota más y fuera";
        TextMeshProUGUI livesInfo = UIKit.Text(lives, "Info", livesText, 18f, losses == 0 ? UIKit.TextSoft : UIKit.Bad,
                                               FontStyles.Normal, TextAlignmentOptions.Left);
        UIKit.Size(livesInfo, -1f, 30f, 1f);

        Divider(panel.transform);

        // Mejoras
        if (TournamentSession.UpgradesEnabled)
        {
            IReadOnlyList<int> ids = TournamentSession.UpgradeIdsOf(player);
            Label(panel.transform, "TUS MEJORAS  (" + ids.Count + ")", UIKit.Accent);
            if (ids.Count == 0) Small(panel.transform, "Las eliges al terminar cada partido");
            else BuildUpgradeList(panel.transform, ids);
            Divider(panel.transform);
        }

        // Resultados simulados desde tu último partido
        Label(panel.transform, "RESULTADOS DE LA JORNADA", UIKit.Accent);
        List<BracketMatch> recent = TournamentSession.LastSimulated;
        if (recent.Count == 0) Small(panel.transform, "Nada nuevo desde tu último partido");
        int shown = 0;
        for (int i = recent.Count - 1; i >= 0 && shown < 4; i--, shown++) ResultRow(panel.transform, b, recent[i]);
        if (recent.Count > 4) Small(panel.transform, "+" + (recent.Count - 4) + " partidos más en el cuadro");

        // Relleno flexible para que todo quede arriba
        RectTransform filler = UIKit.Rect(panel.transform, "Filler");
        UIKit.Size(filler, -1f, 0f).flexibleHeight = 1f;
    }

    // "Patines afilados x2" + sus efectos en una línea
    void BuildUpgradeList(Transform parent, IReadOnlyList<int> ids)
    {
        Dictionary<int, int> counts = new Dictionary<int, int>();
        List<int> order = new List<int>();
        foreach (int id in ids)
        {
            if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); }
            counts[id]++;
        }

        int max = 5;
        for (int i = 0; i < order.Count && i < max; i++)
        {
            UpgradeData u = TournamentSession.Upgrade(order[i]);
            if (u == null) continue;

            RectTransform row = UIKit.Rect(parent, "Upgrade_" + u.DisplayName);
            UIKit.Size(row, -1f, 48f);
            Image dot = UIKit.CircleImage(row, "Rarity", UpgradeDraftPanel.RarityColor(u.Rarity), 12f);
            UIKit.Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(2f, -9f), new Vector2(12f, 12f));

            string title = u.DisplayName + (counts[order[i]] > 1 ? "  <color=#9AA6B8>x" + counts[order[i]] + "</color>" : "");
            TextMeshProUGUI name = UIKit.Text(row, "Name", title, 21f, UIKit.TextMain, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            name.richText = true;
            UIKit.Stretch(name.rectTransform, 24f, 22f, 0f, 0f);

            TextMeshProUGUI fx = UIKit.Text(row, "Effects", UpgradeDraftPanel.ColoredEffects(u).Replace("\n", "  ·  "),
                                            15f, UIKit.TextSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            fx.richText = true;
            UIKit.Stretch(fx.rectTransform, 24f, 0f, 0f, 26f);
        }
        if (order.Count > max) Small(parent, "+" + (order.Count - max) + " más");
    }

    void ResultRow(Transform parent, DoubleEliminationBracket b, BracketMatch m)
    {
        RectTransform row = UIKit.Rect(parent, "Result_" + m.id);
        UIKit.Size(row, -1f, 46f);

        TextMeshProUGUI round = UIKit.Text(row, "Round", b.RoundName(m).ToUpper(), 13f, UIKit.TextFaint, FontStyles.Bold,
                                           TextAlignmentOptions.TopLeft);
        round.characterSpacing = 4f;
        UIKit.Stretch(round.rectTransform, 0f, 26f, 0f, 0f);

        TeamData a = TournamentSession.Team(m.teamA), c = TournamentSession.Team(m.teamB);
        string wa = m.winner == m.teamA ? "<b>" : "<color=#6B7790>", ea = m.winner == m.teamA ? "</b>" : "</color>";
        string wc = m.winner == m.teamB ? "<b>" : "<color=#6B7790>", ec = m.winner == m.teamB ? "</b>" : "</color>";
        string line = $"{wa}{a.TeamName} {m.scoreA}{ea}  –  {wc}{m.scoreB} {c.TeamName}{ec}";
        TextMeshProUGUI t = UIKit.Text(row, "Line", line, 20f, UIKit.TextMain, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        t.richText = true;
        UIKit.Stretch(t.rectTransform, 0f, 0f, 0f, 18f);
    }

    // ---------- Barra inferior: próximo partido ----------

    void BuildBottomBar(TournamentConfig config, BracketMatch next)
    {
        Image bar = UIKit.Image(screen, "BottomBar", UIKit.Panel, 18f);
        RectTransform rt = bar.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(Margin, Margin);
        rt.offsetMax = new Vector2(-Margin, Margin + BottomBarH);

        playButton = UIKit.Button(bar.transform, "Jugar", "JUGAR", true, 40f);
        UIKit.Place((RectTransform)playButton.transform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(360f, 104f));
        playButton.onClick.AddListener(() => PlayClicked?.Invoke());
        DisabledButtonLook.Ensure(playButton, null);

        DoubleEliminationBracket b = TournamentSession.Bracket;
        int player = TournamentSession.PlayerTeam;

        if (b.IsFinished || next == null)
        {
            playButton.interactable = false;
            TeamData champ = b.IsFinished ? TournamentSession.Team(b.championTeam) : null;
            TextMeshProUGUI label = UIKit.Text(bar.transform, "Label", "TORNEO TERMINADO", 18f, UIKit.Accent,
                                               FontStyles.Bold, TextAlignmentOptions.Left);
            label.characterSpacing = 6f;
            UIKit.PlaceTopLeft(label.rectTransform, 32f, 22f, 900f, 28f);
            TextMeshProUGUI line = UIKit.Text(bar.transform, "Champion",
                champ != null ? "Campeón: " + champ.TeamName : "", 40f, UIKit.Gold, FontStyles.Bold, TextAlignmentOptions.Left);
            UIKit.PlaceTopLeft(line.rectTransform, 32f, 58f, 1100f, 60f);

            Button again = UIKit.Button(bar.transform, "Nuevo", "NUEVO TORNEO", false, 28f);
            UIKit.Place((RectTransform)again.transform, new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(360f, 104f));
            again.onClick.AddListener(() => NewTournamentClicked?.Invoke());
            playButton.gameObject.SetActive(false);
            return;
        }

        int rival = next.OpponentOf(player);
        TextMeshProUGUI head = UIKit.Text(bar.transform, "Label", "PRÓXIMO PARTIDO  ·  " + b.RoundName(next).ToUpper(),
                                          18f, UIKit.Accent, FontStyles.Bold, TextAlignmentOptions.Left);
        head.characterSpacing = 6f;
        UIKit.PlaceTopLeft(head.rectTransform, 32f, 18f, 1100f, 28f);

        RectTransform row = UIKit.Rect(bar.transform, "Teams");
        UIKit.PlaceTopLeft(row, 32f, 52f, 1300f, 72f);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 18f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        TeamInline(row, TournamentSession.Team(player), new Color(0.6f, 0.8f, 1f), true);
        TextMeshProUGUI vs = UIKit.Text(row, "VS", "VS", 26f, UIKit.TextFaint, FontStyles.Bold);
        UIKit.Size(vs, 60f, 72f);
        TeamInline(row, TournamentSession.Team(rival), UIKit.TextMain, false);

        if (TournamentSession.UpgradesEnabled)
        {
            int n = TournamentSession.UpgradeIdsOf(rival).Count;
            TextMeshProUGUI info = UIKit.Text(bar.transform, "RivalInfo",
                "El rival lleva " + n + (n == 1 ? " mejora" : " mejoras") + "  ·  Enter para jugar",
                17f, UIKit.TextFaint, FontStyles.Normal, TextAlignmentOptions.Left);
            UIKit.PlaceTopLeft(info.rectTransform, 32f, 126f, 1100f, 24f);
        }

        playButton.interactable = TournamentSession.PlayerPicksPending == 0;
    }

    void TeamInline(RectTransform row, TeamData team, Color color, bool badgeFirst)
    {
        if (badgeFirst) UIKit.Size(UIKit.TeamBadge(row, team, 62f), 62f, 62f);
        TextMeshProUGUI t = UIKit.Text(row, "Name", team.TeamName, 36f, color, FontStyles.Bold,
                                       badgeFirst ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
        t.overflowMode = TextOverflowModes.Overflow;
        UIKit.Size(t, Mathf.Min(440f, t.GetPreferredValues(team.TeamName).x + 8f), 72f);
        if (!badgeFirst) UIKit.Size(UIKit.TeamBadge(row, team, 62f), 62f, 62f);
    }

    // ---------- Capas (overlays) ----------

    public void CloseOverlay()
    {
        if (overlay != null) Destroy(overlay.gameObject);
        overlay = null;
    }

    RectTransform OpenOverlay(float dimAlpha)
    {
        CloseOverlay();
        overlay = UIKit.Rect(root, "Overlay");
        UIKit.Stretch(overlay, 0f);
        Image dim = UIKit.Image(overlay, "Dim", UIKit.Alpha(UIKit.Background, dimAlpha), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);
        return overlay;
    }

    // Red de seguridad: mejoras pendientes al llegar al cuadro (p. ej. se cerró el juego a mitad de elección)
    public void ShowDraft(Action onDone)
    {
        RectTransform o = OpenOverlay(0.92f);
        RectTransform area = UIKit.Rect(o, "Area");
        UIKit.Stretch(area, 0f, 0f, 0f, 200f);
        UpgradeDraftPanel draft = UpgradeDraftPanel.Create(area);
        draft.Completed += () =>
        {
            CloseOverlay();
            onDone?.Invoke();
        };
        draft.Begin();
    }

    public void ShowFinished(TournamentConfig config)
    {
        DoubleEliminationBracket b = TournamentSession.Bracket;
        bool champion = TournamentSession.IsChampion;
        TeamData champ = TournamentSession.Team(b.championTeam);

        RectTransform card = Card(OpenOverlay(0.8f), 900f, 560f);

        TextMeshProUGUI title = UIKit.Text(card, "Title", champion ? "¡CAMPEONES!" : "TORNEO TERMINADO", 64f,
                                           champion ? UIKit.Gold : UIKit.TextMain, FontStyles.Bold);
        title.characterSpacing = 8f;
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(820f, 80f));

        RectTransform badge = UIKit.TeamBadge(card, champ, 130f);
        UIKit.Place(badge, new Vector2(0.5f, 1f), new Vector2(0f, -136f), new Vector2(130f, 130f));

        TextMeshProUGUI name = UIKit.Text(card, "Champion", "Campeón de la " + config.TournamentName + ": " + champ.TeamName,
                                          30f, UIKit.TextMain, FontStyles.Bold);
        UIKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -284f), new Vector2(840f, 44f));

        string mine = champion ? "Has ganado el torneo con " + b.Losses(TournamentSession.PlayerTeam) + " derrotas"
                               : "Tu equipo quedó eliminado. ¡A por la revancha!";
        TextMeshProUGUI sub = UIKit.Text(card, "Mine", mine, 22f, UIKit.TextSoft);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -334f), new Vector2(840f, 34f));

        Button again = UIKit.Button(card, "Nuevo", "NUEVO TORNEO", true, 28f);
        UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(280f, 84f));
        again.onClick.AddListener(() => NewTournamentClicked?.Invoke());

        Button view = UIKit.Button(card, "VerCuadro", "VER CUADRO", false, 28f);
        UIKit.Place((RectTransform)view.transform, new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(280f, 84f));
        view.onClick.AddListener(CloseOverlay);

        UIKit.Select(again);
    }

    public void ShowNoTournament(TournamentConfig config, bool canStart)
    {
        if (screen != null) Destroy(screen.gameObject);
        RectTransform card = Card(OpenOverlay(0f), 900f, 420f);

        TextMeshProUGUI title = UIKit.Text(card, "Title", config != null ? config.TournamentName.ToUpper() : "TORNEO",
                                           56f, UIKit.TextMain, FontStyles.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(820f, 70f));

        string msg;
        string error = null;
        if (config == null) msg = "Asigna un TournamentConfig al TournamentHub";
        else if (!config.IsValid(out error)) msg = error;
        else msg = "No hay ningún torneo en curso";
        TextMeshProUGUI text = UIKit.Text(card, "Message", msg, 26f, UIKit.TextSoft);
        text.textWrappingMode = TextWrappingModes.Normal;
        UIKit.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(820f, 80f));

        Button again = UIKit.Button(card, "Nuevo", "NUEVO TORNEO", true, 28f);
        UIKit.Place((RectTransform)again.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(280f, 84f));
        again.onClick.AddListener(() => NewTournamentClicked?.Invoke());
        again.interactable = canStart;
        DisabledButtonLook.Ensure(again, null);

        Button menu = UIKit.Button(card, "Menu", "MENÚ", false, 28f);
        UIKit.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(280f, 84f));
        menu.onClick.AddListener(() => MenuClicked?.Invoke());

        UIKit.Select(canStart ? again : menu);
    }

    // ---------- Ayudantes ----------

    static RectTransform Card(RectTransform parent, float w, float h)
    {
        Image card = UIKit.Image(parent, "Card", UIKit.Panel, 22f, true);
        UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
        return card.rectTransform;
    }

    static void Label(Transform parent, string text, Color c)
    {
        TextMeshProUGUI t = UIKit.Text(parent, text, text, 17f, c, FontStyles.Bold, TextAlignmentOptions.Left);
        t.characterSpacing = 6f;
        UIKit.Size(t, -1f, 26f);
    }

    static void Small(Transform parent, string text)
    {
        TextMeshProUGUI t = UIKit.Text(parent, "Info", text, 18f, UIKit.TextFaint, FontStyles.Italic, TextAlignmentOptions.Left);
        UIKit.Size(t, -1f, 26f);
    }

    static void Divider(Transform parent)
    {
        Image d = UIKit.Image(parent, "Divider", UIKit.Alpha(Color.white, 0.07f));
        UIKit.Size(d, -1f, 1f);
    }

    static RectTransform Row(Transform parent, float height, float spacing)
    {
        RectTransform row = UIKit.Rect(parent, "Row");
        UIKit.Size(row, -1f, height);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return row;
    }
}
