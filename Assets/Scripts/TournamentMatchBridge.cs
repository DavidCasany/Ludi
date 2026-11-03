using System.Collections;
using UnityEngine;

// Conecta la escena del partido con el torneo:
//  - Al empezar: el jugador (local) es humano y el rival la IA; nombres, colores, nivel de IA y mejoras
//    de los dos equipos; gol de oro activado (en eliminatoria no hay empates).
//  - Al terminar: envía el resultado al torneo y, tras ver un momento el "FIN DEL PARTIDO", abre la
//    pantalla de resultado (TournamentResultScreen), donde se eligen las mejoras y se vuelve al cuadro.
// Si no hay partido de torneo pendiente (amistoso, o la escena abierta directamente) se desactiva solo.
//
// Va antes que TeamManager/GameManager (orden -900) para fijar quién es humano antes de su Awake.
// MatchBootstrapper (-1000) lo desactiva en los amistosos antes de que llegue a suscribirse.
[DefaultExecutionOrder(-900)]
public class TournamentMatchBridge : MonoBehaviour
{
    [SerializeField] GameManager gameManager;
    [Tooltip("Segundos (reales) que se ve el FIN DEL PARTIDO antes de la pantalla de resultado")]
    [SerializeField] float resultDelay = 2f;

    BracketMatch match;
    bool finished;

    void Awake()
    {
        if (gameManager == null) gameManager = GetComponent<GameManager>();
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();

        // OJO: antes se comprobaba 'match' aquí arriba, cuando todavía era null, y el puente se apagaba
        // siempre. Ahora primero se busca el partido y después se decide.
        match = MatchLaunch.Mode == MatchMode.Tournament ? TournamentSession.PendingPlayerMatch() : null;

        if (gameManager == null || gameManager.home == null || gameManager.away == null || match == null)
        {
            enabled = false;
            return;
        }

        gameManager.home.humanControlled = true;    // el jugador siempre es el equipo local
        gameManager.away.humanControlled = false;
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
        int player = TournamentSession.PlayerTeam;
        int rival = match.OpponentOf(player);
        TeamData playerData = TournamentSession.Team(player);
        TeamData rivalData = TournamentSession.Team(rival);

        gameManager.ConfigureMatch(playerData.ShortName, rivalData.ShortName, true);
        TeamDataApplier.ApplyMatch(gameManager.home, playerData, gameManager.away, rivalData);   // equipacions + IA

        // Mejoras roguelite de cada equipo (el rival también ha ido eligiendo)
        TeamStatApplier.Apply(gameManager.home, TournamentSession.UpgradesOf(player));
        TeamStatApplier.Apply(gameManager.away, TournamentSession.UpgradesOf(rival));
    }

    void OnMatchFinished(int homeGoals, int awayGoals)
    {
        if (finished) return;
        finished = true;

        int player = TournamentSession.PlayerTeam;
        TournamentResultScreen.Outcome o = new TournamentResultScreen.Outcome
        {
            playerTeam = TournamentSession.Team(player),
            rivalTeam = TournamentSession.Team(match.OpponentOf(player)),
            playerGoals = homeGoals,
            rivalGoals = awayGoals,
            roundName = TournamentSession.Bracket.RoundName(match)
        };

        if (!TournamentSession.ReportPlayerMatch(homeGoals, awayGoals))
        {
            o.reportFailed = true;
        }
        else
        {
            o.champion = TournamentSession.IsChampion;
            o.eliminated = TournamentSession.PlayerEliminated;
            o.losses = TournamentSession.Bracket.Losses(player);
            BracketMatch next = TournamentSession.PlayerNextMatch();
            o.nextRoundName = next != null ? TournamentSession.Bracket.RoundName(next) : null;
        }

        StartCoroutine(ShowResultAfterDelay(o));
    }

    IEnumerator ShowResultAfterDelay(TournamentResultScreen.Outcome o)
    {
        // Tiempo real: el partido puede haber acabado en plena cámara lenta
        yield return new WaitForSecondsRealtime(resultDelay);
        HudVisibility.Hidden = true;   // el HUD de OnGUI se pinta encima del Canvas: se esconde
        TournamentResultScreen.Show(o);
    }
}
