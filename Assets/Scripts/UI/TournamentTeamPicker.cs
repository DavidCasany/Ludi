using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Finestra per triar amb quin equip es juga el torneig, construïda per codi en el seu propi Canvas
// (com ConfirmDialog). Mostra tots els equips del TournamentConfig en una graella.
//   TournamentTeamPicker.Show(config, team => ..., () => ...);
// Teclat/mando: fletxes per moure's, Enter per triar, Esc = cancel·lar.
public class TournamentTeamPicker : MonoBehaviour
{
    // També val "obert" el frame en què es tanca: així l'Esc que el tanca no el torna a llegir el menú de sota
    public static bool IsOpen => current != null || Time.frameCount == closedFrame;
    static TournamentTeamPicker current;
    static int closedFrame = -1;

    Action<TeamData> onPick;
    Action onCancel;

    public static TournamentTeamPicker Show(TournamentConfig config, Action<TeamData> pick, Action cancel = null)
    {
        if (current != null) Destroy(current.gameObject);

        Canvas canvas = UIKit.CreateCanvas("TournamentTeamPicker", 450);
        UIKit.EnsureEventSystem();
        TournamentTeamPicker p = canvas.gameObject.AddComponent<TournamentTeamPicker>();
        p.onPick = pick;
        p.onCancel = cancel;
        p.Build((RectTransform)canvas.transform, config);
        current = p;
        return p;
    }

    void Build(RectTransform root, TournamentConfig config)
    {
        IReadOnlyList<TeamData> teams = config.Teams;
        int count = teams.Count;
        int columns = 4;
        int rows = Mathf.CeilToInt(count / (float)columns);
        bool compact = count > 8;
        Vector2 cell = compact ? new Vector2(290f, 120f) : new Vector2(290f, 230f);
        float spacing = 22f;
        float gridH = rows * cell.y + (rows - 1) * spacing;

        Image dim = UIKit.Image(root, "Dim", UIKit.Alpha(Color.black, 0.7f), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);

        float cardH = 150f + gridH + 150f;
        Image card = UIKit.SteelPanel(root, "Card", 36f, true);
        UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1360f, cardH));

        Image bar = UIKit.Image(card.transform, "Accent", UIKit.Accent, 3f);
        UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(90f, 6f));

        TextMeshProUGUI title = UIKit.Text(card.transform, "Title", "TRIA EL TEU EQUIP", 46f, UIKit.TextMain, FontStyles.Bold);
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1200f, 64f));

        // Graella d'equips
        RectTransform grid = UIKit.Rect(card.transform, "Teams");
        float gridW = columns * cell.x + (columns - 1) * spacing;
        UIKit.Place(grid, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(gridW, gridH));
        GridLayoutGroup g = grid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = cell;
        g.spacing = new Vector2(spacing, spacing);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = columns;
        g.childAlignment = TextAnchor.UpperCenter;

        Button first = null;
        foreach (TeamData team in teams)
        {
            Button b = Tile(grid, team, compact);
            if (first == null || team == config.PlayerTeam) first = b;
        }

        Button cancel = UIKit.Button(card.transform, "Cancel", "CANCEL·LAR", false, 28f);
        UIKit.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(320f, 84f));
        cancel.onClick.AddListener(Cancel);

        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        if (first != null) UIKit.Select(first);
    }

    Button Tile(Transform parent, TeamData team, bool compact)
    {
        Image bg = UIKit.Image(parent, team.TeamName, Color.white, 18f, true);
        Button b = bg.gameObject.AddComponent<Button>();
        b.targetGraphic = bg;
        b.colors = UIKit.Colors(UIKit.Surface, UIKit.SurfaceHover, UIKit.Accent);
        b.onClick.AddListener(() => Pick(team));
        bg.gameObject.AddComponent<HoverPop>();

        float badge = compact ? 76f : 130f;
        RectTransform badgeRt = UIKit.TeamBadge(bg.transform, team, badge);
        TextMeshProUGUI name = UIKit.Text(bg.transform, "Name", team.TeamName.ToUpperInvariant(),
                                          compact ? 24f : 28f, UIKit.TextMain, FontStyles.Bold);
        if (compact)
        {
            UIKit.Place(badgeRt, new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(badge, badge));
            name.alignment = TextAlignmentOptions.Left;
            UIKit.Stretch(name.rectTransform, badge + 32f, 0f, 12f, 0f);
        }
        else
        {
            UIKit.Place(badgeRt, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(badge, badge));
            UIKit.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(270f, 44f));
        }
        return b;
    }

    void Update()
    {
        if (UIKit.BackPressed()) Cancel();
    }

    void Pick(TeamData team)
    {
        Action<TeamData> a = onPick;
        Close();
        a?.Invoke(team);
    }

    void Cancel()
    {
        Action a = onCancel;
        Close();
        a?.Invoke();
    }

    void Close()
    {
        if (current == this) current = null;
        closedFrame = Time.frameCount;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }
}
