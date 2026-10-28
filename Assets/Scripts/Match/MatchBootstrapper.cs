using UnityEngine;

// Prepara un partido amistoso (1vs1) a partir de un MatchSetup.
//
// Orden de prioridad al cargar la escena:
//   1. Hay un partido lanzado con MatchLauncher  -> lo monta y desactiva el TournamentMatchBridge.
//   2. Hay un partido de torneo pendiente        -> no hace nada (lo monta TournamentMatchBridge).
//   3. Hay un setup de prueba válido aquí abajo  -> lo usa (para dar Play directamente a la escena).
//   4. Nada de lo anterior                       -> no hace nada: la escena funciona como antes.
//
// Se ejecuta ANTES que el resto (DefaultExecutionOrder) porque TeamManager.Awake y GameManager.Awake
// leen "humanControlled", así que hay que fijarlo antes de que ellos despierten.
[DefaultExecutionOrder(-1000)]
public class MatchBootstrapper : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    [Header("Partido de prueba (solo si se abre la escena directamente)")]
    [SerializeField] MatchSetup debugSetup = new MatchSetup();

    MatchSetup setup;
    bool reported;

    public MatchSetup Setup => setup;

    void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager == null || gameManager.home == null || gameManager.away == null)
        {
            Debug.LogWarning("MatchBootstrapper: falta el GameManager o sus equipos", this);
            enabled = false;
            return;
        }

        setup = MatchLauncher.TakePending();

        if (setup != null)
        {
            DisableTournamentBridge();   // un amistoso nunca debe reportar al torneo
        }
        else if (TournamentSession.PendingPlayerMatch() != null)
        {
            enabled = false;             // partido de torneo: lo gestiona TournamentMatchBridge
            return;
        }
        else if (debugSetup != null && debugSetup.homeTeam != null && debugSetup.awayTeam != null)
        {
            setup = debugSetup;
        }

        string error = null;
        if (setup == null || !setup.IsValid(out error))
        {
            if (setup != null) Debug.LogWarning("MatchBootstrapper: " + error, this);
            enabled = false;
            return;
        }

        ApplyController(gameManager.home, setup.homeController);
        ApplyController(gameManager.away, setup.awayController);
    }

    void OnEnable()  { if (gameManager != null) gameManager.MatchFinished += OnMatchFinished; }
    void OnDisable() { if (gameManager != null) gameManager.MatchFinished -= OnMatchFinished; }

    void Start()
    {
        // En Start: TeamManager.Awake ya ha rellenado sus listas de jugadores
        gameManager.ConfigureMatch(setup.homeTeam.ShortName, setup.awayTeam.ShortName, setup.requireWinner);

        TeamDataApplier.Apply(gameManager.home, setup.homeTeam);
        TeamDataApplier.Apply(gameManager.away, setup.awayTeam);

        TeamStatApplier.Apply(gameManager.home, setup.homeUpgrades);
        TeamStatApplier.Apply(gameManager.away, setup.awayUpgrades);
    }

    static void ApplyController(TeamManager tm, ControllerType type)
    {
        if (type == ControllerType.HumanP2)
        {
            // Punto de enganche del PvP: cuando exista el input del jugador 2, se conecta aquí
            Debug.LogWarning("MatchBootstrapper: PvP aún no implementado, " + tm.name + " lo controlará la CPU");
            type = ControllerType.CPU;
        }
        tm.humanControlled = type == ControllerType.HumanP1;
    }

    void DisableTournamentBridge()
    {
        // Se desactiva antes de su Awake/OnEnable, así nunca llega a suscribirse ni a reportar
        foreach (TournamentMatchBridge b in FindObjectsByType<TournamentMatchBridge>(FindObjectsSortMode.None))
            b.enabled = false;
    }

    void OnMatchFinished(int homeGoals, int awayGoals)
    {
        if (reported) return;
        reported = true;
        MatchLauncher.ReportResult(new MatchResult(setup, homeGoals, awayGoals));
    }
}
