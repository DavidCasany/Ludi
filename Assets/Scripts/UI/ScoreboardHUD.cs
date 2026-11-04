using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Marcador del partit (dalt al centre) i cartells grans (GOL!, DESCANS, EMPAT, FINAL) amb l'estil de
// l'art de l'artista: marc metàl·lic com ButtonFrame i paper crema amb contorn com UpgradeCard.
// Peces a Assets/Resources/Art/HUD/ (generades amb Tools/gen_hud_sprites.py). Si falten, rectangles.
//
//   ┌──────────────────────┬─────────┬──────────────────────┐
//   │ [escut]  BARÇA       │  2 - 1  │       LICEO  [escut] │
//   └──────────────────────┴─────────┴──────────────────────┘
//                         [1a PART 02:34]
//
// L'escut va sobre una franja amb el color principal de l'equip. Local sempre a l'esquerra.
// El crea GameManager.Awake; no cal posar res a l'escena.
public class ScoreboardHUD : MonoBehaviour
{
    const string ArtPath = "Art/HUD/";

    // Geometria de hud_board.png (en píxels, origen a dalt a l'esquerra)
    const float BoardW = 868f, BoardH = 140f;
    const float WinTop = 20f, WinBottom = 118f;                  // finestres de paper
    const float LeftX0 = 22f, LeftX1 = 322f;
    const float ScoreX0 = 334f, ScoreX1 = 534f;
    const float RightX0 = 546f, RightX1 = 846f;
    const float BandW = 120f, BandH = 88f;                       // franja de l'equip
    const float BandMargin = (WinBottom - WinTop - BandH) * 0.5f; // igual a dalt i a baix
    const float CrestMargin = 8f;                                // escut dins la franja
    const float ClockW = 220f, ClockH = 50f;

    // Mida del marcador a la pantalla (referència 1920x1080). 0.8 = uns 700 px d'ample
    const float BoardScale = 0.8f;

    // Colors de l'art (UpgradeCard / ButtonFrame)
    static readonly Color Ink = new Color32(45, 56, 64, 255);
    static readonly Color Paper = new Color32(243, 234, 218, 255);
    static readonly Color Red = new Color32(167, 68, 59, 255);
    static readonly Color FallbackHome = new Color32(64, 110, 170, 255);
    static readonly Color FallbackAway = new Color32(167, 68, 59, 255);

    GameManager gm;
    CanvasGroup rootGroup;

    Image homeBand, awayBand, homeCrest, awayCrest;
    TextMeshProUGUI homeName, awayName, homeInitials, awayInitials, score, clock;
    RectTransform scoreRT;

    RectTransform banner;
    CanvasGroup bannerGroup;
    Image bannerBand;
    TextMeshProUGUI bannerTitle, bannerSub;

    CanvasGroup hintGroup;
    TextMeshProUGUI hintText;

    TeamData shownHome, shownAway;
    string shownHomeName, shownAwayName;
    int lastHome = -1, lastAway = -1;
    Team lastScorer = Team.Home;
    MatchState lastState = MatchState.CoinToss;
    float stateTime, scorePop;

    public static ScoreboardHUD Create(GameManager gm)
    {
        Canvas canvas = UIKit.CreateCanvas("ScoreboardHUD", 40);
        ScoreboardHUD hud = canvas.gameObject.AddComponent<ScoreboardHUD>();
        hud.gm = gm;
        hud.Build(canvas.transform);
        return hud;
    }

    static Image ArtImage(Transform parent, string name, string sprite, Color color, float fallbackRadius)
    {
        Image img = UIKit.Image(parent, name, color);
        Sprite s = Resources.Load<Sprite>(ArtPath + sprite);
        if (s != null)
        {
            img.sprite = s;
            img.color = Color.white;   // l'art ja porta els seus colors (les franges es tenyeixen després)
        }
        else UIKit.SetRounded(img, fallbackRadius);
        return img;
    }

    // Rectangle dins del marcador amb coordenades de la imatge (x0,y0 a dalt a l'esquerra)
    static void PlaceIn(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x0, -y0);
        rt.sizeDelta = new Vector2(x1 - x0, y1 - y0);
    }

    void Build(Transform canvas)
    {
        RectTransform root = UIKit.Rect(canvas, "Root");
        UIKit.Stretch(root, 0f);
        rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        rootGroup.alpha = 0f;

        // ---------- Marcador ----------
        RectTransform board = UIKit.Rect(root, "Scoreboard");
        UIKit.Place(board, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(BoardW, BoardH));
        board.localScale = new Vector3(BoardScale, BoardScale, 1f);

        // Pestanya del rellotge (primer: queda per sota del marc)
        Image clockTab = ArtImage(board, "ClockTab", "hud_clock", new Color32(82, 103, 129, 255), 14f);
        PlaceIn(clockTab.rectTransform, (BoardW - ClockW) * 0.5f, BoardH - 14f, (BoardW + ClockW) * 0.5f, BoardH - 14f + ClockH);
        clock = UIKit.Text(clockTab.transform, "Clock", "", 22f, Paper, FontStyles.Bold);
        clock.characterSpacing = 1f;
        clock.enableAutoSizing = true;   // "1a PART  03:00" sempre hi cap sencer
        clock.fontSizeMin = 14f;
        clock.fontSizeMax = 22f;
        UIKit.Stretch(clock.rectTransform, 26f, 10f, 26f, 9f);

        Image frame = ArtImage(board, "Board", "hud_board", new Color32(82, 103, 129, 255), 30f);
        UIKit.Stretch(frame.rectTransform, 0f);

        BuildTeam(board, true, out homeBand, out homeCrest, out homeName, out homeInitials);
        BuildTeam(board, false, out awayBand, out awayCrest, out awayName, out awayInitials);

        score = UIKit.Text(board, "Score", "0 - 0", 68f, Ink, FontStyles.Bold);
        score.alignment = TextAlignmentOptions.Center;
        scoreRT = score.rectTransform;
        PlaceIn(scoreRT, ScoreX0, WinTop, ScoreX1, WinBottom);

        // ---------- Cartell central ----------
        banner = UIKit.Rect(root, "Banner");
        UIKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(820f, 230f));
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        Image bannerBg = ArtImage(banner, "Paper", "hud_banner", Paper, 26f);
        UIKit.Stretch(bannerBg.rectTransform, 0f);
        bannerBand = ArtImage(banner, "Band", "hud_banner_band", Red, 12f);
        UIKit.Stretch(bannerBand.rectTransform, 0f);
        bannerTitle = UIKit.Text(banner, "Title", "", 120f, Ink, FontStyles.Bold);
        bannerTitle.characterSpacing = 6f;
        PlaceIn(bannerTitle.rectTransform, 40f, 18f, 780f, 158f);
        bannerSub = UIKit.Text(banner, "Sub", "", 28f, Color.white, FontStyles.Bold);
        PlaceIn(bannerSub.rectTransform, 50f, 160f, 770f, 196f);

        // ---------- Avís del saque (a baix) ----------
        Image hintBg = ArtImage(root, "KickoffHint", "hud_banner", Paper, 18f);
        UIKit.Place(hintBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(760f, 84f));
        hintGroup = hintBg.gameObject.AddComponent<CanvasGroup>();
        hintGroup.alpha = 0f;
        hintText = UIKit.Text(hintBg.transform, "Text", "", 30f, Ink, FontStyles.Bold);
        UIKit.Stretch(hintText.rectTransform, 24f, 14f, 24f, 10f);

        RefreshTeams(true);
    }

    // Finestra d'un equip: franja de color amb l'escut al costat de fora + nom a la resta
    void BuildTeam(RectTransform board, bool left, out Image band, out Image crest,
                   out TextMeshProUGUI nameText, out TextMeshProUGUI initials)
    {
        float x0 = left ? LeftX0 : RightX0, x1 = left ? LeftX1 : RightX1;
        float by0 = WinTop + BandMargin, by1 = by0 + BandH;
        float bx0 = left ? x0 + BandMargin : x1 - BandMargin - BandW;

        band = ArtImage(board, left ? "HomeBand" : "AwayBand", "hud_band", Color.white, 12f);
        PlaceIn(band.rectTransform, bx0, by0, bx0 + BandW, by1);
        Image line = ArtImage(board, left ? "HomeBandLine" : "AwayBandLine", "hud_band_line", Color.white, 12f);
        if (line.sprite == UIKit.Rounded) line.enabled = false;   // sense l'art no cal el contorn
        PlaceIn(line.rectTransform, bx0, by0, bx0 + BandW, by1);

        // Escut centrat a la franja, amb el mateix marge pels quatre costats
        crest = UIKit.Image(board, left ? "HomeCrest" : "AwayCrest", Color.white);
        crest.preserveAspect = true;
        PlaceIn(crest.rectTransform, bx0 + CrestMargin, by0 + CrestMargin, bx0 + BandW - CrestMargin, by1 - CrestMargin);

        initials = UIKit.Text(board, left ? "HomeInitials" : "AwayInitials", "", 30f, Color.white, FontStyles.Bold);
        PlaceIn(initials.rectTransform, bx0, by0, bx0 + BandW, by1);

        nameText = UIKit.Text(board, left ? "HomeName" : "AwayName", "", 40f, Ink, FontStyles.Bold);
        nameText.characterSpacing = 2f;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 20f;
        nameText.fontSizeMax = 40f;
        nameText.alignment = TextAlignmentOptions.Center;
        // Nom centrat a l'espai que queda entre la franja i la vora de dins (mateix marge que la franja)
        float nx0 = left ? bx0 + BandW + BandMargin : x0 + BandMargin;
        float nx1 = left ? x1 - BandMargin : bx0 - BandMargin;
        PlaceIn(nameText.rectTransform, nx0, WinTop, nx1, WinBottom);
    }

    void Update()
    {
        if (gm == null) { Destroy(gameObject); return; }

        bool hidden = PauseController.IsPaused || HudVisibility.Hidden ||
                      gm.State == MatchState.CoinToss || gm.State == MatchState.CoinResult;
        rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, hidden ? 0f : 1f, Time.unscaledDeltaTime * 6f);

        RefreshTeams(false);
        UpdateScore();
        UpdateClock();
        UpdateState();
    }

    void RefreshTeams(bool force)
    {
        if (!force && shownHome == gm.HomeData && shownAway == gm.AwayData &&
            shownHomeName == gm.HomeName && shownAwayName == gm.AwayName) return;

        shownHome = gm.HomeData;
        shownAway = gm.AwayData;
        shownHomeName = gm.HomeName;
        shownAwayName = gm.AwayName;

        SetTeam(shownHome, shownHomeName, FallbackHome, homeBand, homeCrest, homeName, homeInitials);
        SetTeam(shownAway, shownAwayName, FallbackAway, awayBand, awayCrest, awayName, awayInitials);
    }

    static void SetTeam(TeamData data, string name, Color fallback, Image band, Image crest,
                        TextMeshProUGUI nameText, TextMeshProUGUI initials)
    {
        Color c = data != null ? data.PrimaryColor : fallback;
        band.color = c;
        nameText.text = string.IsNullOrEmpty(name) ? "" : name.ToUpperInvariant();

        bool hasCrest = data != null && data.Crest != null;
        crest.enabled = hasCrest;
        if (hasCrest) crest.sprite = data.Crest;

        initials.enabled = !hasCrest;
        string shortName = data != null ? data.ShortName : name;
        initials.text = string.IsNullOrEmpty(shortName) ? "" : shortName.Substring(0, Mathf.Min(3, shortName.Length)).ToUpperInvariant();
        initials.color = Luma(c) > 0.62f ? Ink : Color.white;
    }

    static float Luma(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

    void UpdateScore()
    {
        if (gm.homeScore != lastHome || gm.awayScore != lastAway)
        {
            bool first = lastHome < 0;
            if (!first && gm.homeScore > lastHome) lastScorer = Team.Home;
            else if (!first && gm.awayScore > lastAway) lastScorer = Team.Away;
            lastHome = gm.homeScore;
            lastAway = gm.awayScore;
            score.text = $"{lastHome} - {lastAway}";
            if (!first) scorePop = 1f;
        }

        // Cop d'escala quan entra un gol
        scorePop = Mathf.MoveTowards(scorePop, 0f, Time.unscaledDeltaTime * 2.2f);
        float s = 1f + Mathf.Sin(scorePop * Mathf.PI) * 0.22f;
        scoreRT.localScale = new Vector3(s, s, 1f);
    }

    void UpdateClock()
    {
        clock.color = Paper;
        switch (gm.State)
        {
            case MatchState.HalfTime: clock.text = "DESCANS"; return;
            case MatchState.FullTime: clock.text = "FINAL"; return;
        }
        if (gm.IsGoldenGoal) { clock.text = "GOL D'OR"; return; }

        int t = Mathf.CeilToInt(Mathf.Max(0f, gm.TimeLeft));
        clock.text = $"{gm.Half}a PART  {t / 60:00}:{t % 60:00}";
        // L'últim mig minut, en vermell (el vermell de la porteria de la targeta, aclarit)
        if (gm.State == MatchState.Playing && t <= 30) clock.color = new Color32(235, 120, 100, 255);
    }

    void UpdateState()
    {
        MatchState st = gm.State;
        if (st != lastState)
        {
            lastState = st;
            stateTime = 0f;
            ShowBannerFor(st);
        }
        stateTime += Time.unscaledDeltaTime;

        bool showBanner = st == MatchState.GoalPause || st == MatchState.HalfTime ||
                          st == MatchState.OvertimeBreak || st == MatchState.FullTime;
        bannerGroup.alpha = Mathf.MoveTowards(bannerGroup.alpha, showBanner ? 1f : 0f, Time.unscaledDeltaTime * 5f);

        // Entrada amb rebot: comença gran i s'assenta
        float k = Mathf.Clamp01(stateTime / 0.35f);
        float scale = showBanner ? Mathf.Lerp(1.3f, 1f, 1f - (1f - k) * (1f - k)) : 1f;
        banner.localScale = new Vector3(scale, scale, 1f);

        bool kickoff = st == MatchState.Kickoff;
        if (kickoff)
            hintText.text = gm.KickoffTeam == gm.PlayerTeam
                ? "TREUS TU: passa-la a un company (clic dret)"
                : "Treu el rival";
        hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, kickoff ? 1f : 0f, Time.unscaledDeltaTime * 5f);
    }

    void ShowBannerFor(MatchState st)
    {
        string result = $"{gm.HomeName}  {gm.homeScore} - {gm.awayScore}  {gm.AwayName}".ToUpperInvariant();
        switch (st)
        {
            case MatchState.GoalPause:
            {
                TeamData d = lastScorer == Team.Home ? gm.HomeData : gm.AwayData;
                Color c = d != null ? d.PrimaryColor : (lastScorer == Team.Home ? FallbackHome : FallbackAway);
                SetBanner(gm.IsGoldenGoal ? "GOL D'OR!" : "GOL!", c, result);
                break;
            }
            case MatchState.HalfTime:
                SetBanner("DESCANS", new Color32(82, 103, 129, 255), result);
                break;
            case MatchState.OvertimeBreak:
                SetBanner("EMPAT", Red, "PRÒRROGA A GOL D'OR: QUI MARQUI PRIMER GUANYA");
                break;
            case MatchState.FullTime:
            {
                string who = gm.homeScore > gm.awayScore ? "GUANYA " + gm.HomeName
                           : gm.awayScore > gm.homeScore ? "GUANYA " + gm.AwayName
                           : "EMPAT";
                SetBanner("FINAL", Red, $"{gm.homeScore} - {gm.awayScore}   {who}".ToUpperInvariant());
                break;
            }
        }
    }

    void SetBanner(string title, Color bandColor, string sub)
    {
        bannerBand.color = bandColor;
        bannerTitle.text = title;
        bannerSub.text = sub;
        bannerSub.color = Luma(bandColor) > 0.62f ? Ink : Color.white;
    }
}
