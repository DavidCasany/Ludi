using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla que aparece al acabar un partido del torneo (la abre TournamentMatchBridge):
//   ronda · ¡VICTORIA! / DERROTA / ELIMINADO / ¡CAMPEONES! · marcador con los dos escudos · qué pasa ahora
//   + las cartas de mejora para el siguiente partido (si te toca elegir)
//   + VER EL CUADRO, que se activa cuando ya has elegido.
// Se construye por código en un Canvas propio (por encima del partido y por debajo de Ajustes).
public class TournamentResultScreen : MonoBehaviour
{
    public class Outcome
    {
        public TeamData playerTeam, rivalTeam;
        public int playerGoals, rivalGoals;
        public string roundName;
        public string nextRoundName;   // null si no hay siguiente partido (campeón o eliminado)
        public int losses;
        public bool champion, eliminated, reportFailed;
        public bool Won => playerGoals > rivalGoals;
    }

    const float InputDelay = 0.8f;   // para no pulsar sin querer con un clic de la jugada

    Outcome outcome;
    CanvasGroup group;
    Button continueButton;
    UpgradeDraftPanel draft;
    float shownAt;
    float readyAt;   // momento a partir del cual Enter vale para continuar
    bool leaving;

    public static TournamentResultScreen Show(Outcome o)
    {
        Canvas canvas = UIKit.CreateCanvas("TournamentResult", 150);
        TournamentResultScreen screen = canvas.gameObject.AddComponent<TournamentResultScreen>();
        screen.outcome = o;
        screen.Build((RectTransform)canvas.transform);
        return screen;
    }

    void Build(RectTransform root)
    {
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        shownAt = Time.unscaledTime;
        readyAt = shownAt + InputDelay;

        Image dim = UIKit.Image(root, "Background", UIKit.Alpha(UIKit.Background, 0.95f), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);

        Outcome o = outcome;
        bool drafting = !o.reportFailed && TournamentSession.PlayerPicksPending > 0;

        // ---------- Cabecera: ronda y resultado ----------
        TextMeshProUGUI round = UIKit.Text(root, "Round", (o.roundName ?? "").ToUpper(), 22f, UIKit.Accent, FontStyles.Bold);
        round.characterSpacing = 10f;
        UIKit.Place(round.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 30f));

        string word; Color wordColor;
        if (o.champion) { word = "CAMPIONS!"; wordColor = UIKit.Gold; }
        else if (o.eliminated) { word = "ELIMINAT"; wordColor = UIKit.Bad; }
        else if (o.Won) { word = "VICTÒRIA!"; wordColor = UIKit.Good; }
        else { word = "DERROTA"; wordColor = UIKit.Bad; }

        TextMeshProUGUI title = UIKit.Text(root, "Result", word, drafting ? 76f : 110f, wordColor, FontStyles.Bold);
        title.characterSpacing = 8f;
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -72f),
                    new Vector2(1400f, drafting ? 90f : 130f));

        // ---------- Marcador ----------
        float scoreY = drafting ? -170f : -230f;
        RectTransform score = UIKit.Rect(root, "Score");
        UIKit.Place(score, new Vector2(0.5f, 1f), new Vector2(0f, scoreY), new Vector2(1300f, 90f));
        BuildScoreRow(score, o);

        // ---------- Qué pasa ahora ----------
        TextMeshProUGUI status = UIKit.Text(root, "Status", StatusText(o), 28f, UIKit.TextSoft);
        status.textWrappingMode = TextWrappingModes.Normal;
        UIKit.Place(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, scoreY - 100f), new Vector2(1400f, 40f));

        if (!drafting && !o.reportFailed && !o.champion && !o.eliminated)
        {
            TextMeshProUGUI lives = UIKit.Text(root, "Losses", LossesText(o.losses), 24f, UIKit.TextFaint);
            UIKit.Place(lives.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, scoreY - 146f), new Vector2(800f, 34f));
        }

        // ---------- Mejoras ----------
        if (drafting)
        {
            RectTransform area = UIKit.Rect(root, "DraftArea");
            UIKit.Stretch(area, 0f, 0f, 0f, 325f);   // debajo del marcador
            draft = UpgradeDraftPanel.Create(area);
            draft.Completed += OnDraftCompleted;
        }

        // ---------- Volver al cuadro ----------
        continueButton = UIKit.Button(root, "VerCuadro", "VEURE EL QUADRE", true, 34f);
        UIKit.Place((RectTransform)continueButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 48f),
                    new Vector2(440f, 96f));
        continueButton.onClick.AddListener(GoToBracket);
        DisabledButtonLook.Ensure(continueButton, "TRIA UNA MILLORA");
        continueButton.interactable = !drafting;

        TextMeshProUGUI hint = UIKit.Text(root, "Hint", "Enter · Continuar", 20f, UIKit.TextFaint);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(400f, 28f));

        if (draft != null) draft.Begin();   // puede completarse en el acto si no quedan mejoras disponibles
        else UIKit.Select(continueButton);
    }

    void BuildScoreRow(RectTransform row, Outcome o)
    {
        const float badge = 84f;

        TextMeshProUGUI goals = UIKit.Text(row, "Goals", $"{o.playerGoals}  –  {o.rivalGoals}", 80f, UIKit.TextMain,
                                           FontStyles.Bold);
        UIKit.Place(goals.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 90f));

        RectTransform left = UIKit.TeamBadge(row, o.playerTeam, badge);
        UIKit.Place(left, new Vector2(0.5f, 0.5f), new Vector2(-210f, 0f), new Vector2(badge, badge));
        TextMeshProUGUI leftName = UIKit.Text(row, "PlayerName", o.playerTeam != null ? o.playerTeam.TeamName : "",
                                              36f, UIKit.TextMain, FontStyles.Bold, TextAlignmentOptions.Right);
        UIKit.Place(leftName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-480f, 0f), new Vector2(400f, 60f));

        RectTransform right = UIKit.TeamBadge(row, o.rivalTeam, badge);
        UIKit.Place(right, new Vector2(0.5f, 0.5f), new Vector2(210f, 0f), new Vector2(badge, badge));
        TextMeshProUGUI rightName = UIKit.Text(row, "RivalName", o.rivalTeam != null ? o.rivalTeam.TeamName : "",
                                               36f, UIKit.TextSoft, FontStyles.Bold, TextAlignmentOptions.Left);
        UIKit.Place(rightName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(480f, 0f), new Vector2(400f, 60f));
    }

    static string StatusText(Outcome o)
    {
        if (o.reportFailed) return "No s'ha pogut registrar el resultat (mira la consola).";
        if (o.champion)
        {
            string cup = TournamentSession.Config != null ? TournamentSession.Config.TournamentName : "el torneig";
            return "Ets el campió de la " + cup + "!";
        }
        if (o.eliminated) return "Segona derrota: quedes eliminat del torneig";

        string next = string.IsNullOrEmpty(o.nextRoundName) ? "" : "   ·   Següent: " + o.nextRoundName;
        if (!o.Won) return "Primera derrota: segueixes viu, però ja no pots fallar" + next;
        return (o.losses == 0 ? "Segueixes invicte" : "Segueixes viu al quadre de perdedors") + next;
    }

    static string LossesText(int losses) =>
        losses == 0 ? "Derrotes: 0 / 2" : "Derrotes: " + losses + " / 2  ·  una més i quedes fora";

    void OnDraftCompleted()
    {
        // El Enter que eligió la última carta no debe saltar también esta pantalla
        readyAt = Mathf.Max(readyAt, Time.unscaledTime + 0.35f);
        continueButton.interactable = true;
        UIKit.Select(continueButton);
    }

    void Update()
    {
        if (group.alpha < 1f) group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime / 0.25f);
        if (leaving || Time.unscaledTime < readyAt) return;

        if (continueButton.interactable && ConfirmPressed()) GoToBracket();
    }

    void GoToBracket()
    {
        if (leaving || !continueButton.interactable) return;

        string hub = TournamentSession.Config != null ? TournamentSession.Config.HubScene : "";
        if (string.IsNullOrEmpty(hub) || !Application.CanStreamedLevelBeLoaded(hub))
        {
            Debug.LogError($"[Torneo] No se puede cargar el cuadro '{hub}': añádelo a Build Profiles.", this);
            return;
        }
        leaving = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(hub);
    }

    static bool ConfirmPressed()
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
