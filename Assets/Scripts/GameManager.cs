using System;
using System.Collections;
using UnityEngine;

public enum MatchState
{
    CoinToss,       // el jugador elige cara o cruz
    CoinResult,     // se muestra el resultado del sorteo
    Kickoff,        // saque de centro: reloj parado hasta el primer pase
    Playing,        // el reloj corre (o, en el gol de oro, se juega sin reloj)
    GoalPause,      // gol: reloj parado y jugadores quietos
    HalfTime,       // fin de la 1ª parte
    OvertimeBreak,  // empate al final y hace falta un ganador: pausa antes del gol de oro
    FullTime        // fin del partido
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Equipos y bola")]
    public TeamManager home;
    public TeamManager away;
    public Ball ball;
    public Vector2 ballStart = Vector2.zero;
    public EdgeCollider2D rinkWalls;    // objeto Pista (red de seguridad contra bolas fuera)

    [Header("Tiempo de partido")]
    public float halfDuration = 180f;   // 3 minutos por parte
    public float resetDelay = 2f;       // pausa tras un gol
    public float halfTimeBreak = 4f;    // pausa del descanso
    public float overtimeBreak = 3f;    // pausa antes del gol de oro
    public float coinResultTime = 2.5f; // cuánto se ve el resultado del sorteo (solo con el sorteo de OnGUI)

    [Header("Sorteo")]
    [Tooltip("Usa la pantalla de sorteo con la moneda animada (CoinTossScreen) en vez de la de OnGUI")]
    public bool useCoinTossScreen = true;
    [Tooltip("Opcional: sonido al lanzar la moneda")]
    public AudioClip coinFlipClip;
    [Tooltip("Opcional: sonido al caer la moneda")]
    public AudioClip coinLandClip;

    [Header("Marcador")]
    public int homeScore;
    public int awayScore;
    [SerializeField] string homeName = "HOME";
    [SerializeField] string awayName = "AWAY";

    [Header("Reglas")]
    [Tooltip("Si acaba en empate se juega una prórroga a gol de oro (lo activa el torneo)")]
    [SerializeField] bool requireWinner = false;

    public MatchState State { get; private set; } = MatchState.CoinToss;
    public int Half { get; private set; } = 1;
    public bool IsGoldenGoal { get; private set; }
    public string HomeName => homeName;
    public string AwayName => awayName;
    public Team PlayerTeam => playerTeam;
    public float TimeLeft => timeLeft;
    public Team KickoffTeam => kickoffTeam;

    // Datos de los equipos (escudo y colores para el marcador). Null si el partido no los ha recibido.
    public TeamData HomeData { get; private set; }
    public TeamData AwayData { get; private set; }

    ScoreboardHUD scoreboard;   // marcador y carteles (GOL!, DESCANS...) con el estilo del arte

    // Sorteo dibujado por otra pantalla (CoinTossScreen): GameManager decide el resultado y espera
    // a que la pantalla llame a CompleteCoinToss() para empezar el saque
    public bool CoinTossHandledExternally { get; set; }
    // (salió cara, ganó el jugador)
    public event Action<bool, bool> CoinTossed;

    // Se lanza una sola vez al terminar el partido: (goles local, goles visitante)
    public event Action<int, int> MatchFinished;

    float timeLeft;
    Goal[] goals;

    // Sorteo
    Team playerTeam;        // el equipo controlado por el humano
    Team firstServer;       // quién saca en la 1ª parte
    string coinResultText = "";
    string coinServeText = "";

    // Saque de centro
    Team kickoffTeam;
    Skater kickoffServer;

    void Awake()
    {
        Instance = this;
        goals = FindObjectsByType<Goal>(FindObjectsSortMode.None);
        timeLeft = halfDuration;
        playerTeam = home.humanControlled ? Team.Home : Team.Away;
        if (useCoinTossScreen) CoinTossScreen.Create(this);
        scoreboard = ScoreboardHUD.Create(this);
    }

    // Lo llama quien organiza el partido (p. ej. el torneo) antes de que empiece
    public void ConfigureMatch(TeamData homeTeam, TeamData awayTeam, bool mustHaveWinner)
    {
        HomeData = homeTeam;
        AwayData = awayTeam;
        ConfigureMatch(homeTeam != null ? homeTeam.ShortName : homeName,
                       awayTeam != null ? awayTeam.ShortName : awayName, mustHaveWinner);
    }

    public void ConfigureMatch(string homeTeamName, string awayTeamName, bool mustHaveWinner)
    {
        homeName = homeTeamName;
        awayName = awayTeamName;
        requireWinner = mustHaveWinner;
    }

    void Update()
    {
        CheckBallInBounds();

        // El reloj solo corre mientras se juega (no en el saque, ni en pausas, ni en el gol de oro)
        if (State != MatchState.Playing || IsGoldenGoal) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            EndOfHalf();
        }
    }

    // ---------- Reglas que consulta Skater ----------

    public bool CanShoot => State == MatchState.Playing;
    public bool CanPass => State == MatchState.Playing || State == MatchState.Kickoff;

    public bool CanMove(Skater s) => State == MatchState.Playing;

    public bool IsServing(Skater s) => State == MatchState.Kickoff && s == kickoffServer;

    // Lo llama Skater.PassTo: el primer pase del sacador pone el juego en marcha
    public void OnPass(Skater passer)
    {
        if (IsServing(passer)) State = MatchState.Playing;
    }

    // ---------- Sorteo ----------

    public void ChooseCoin(bool playerPicksHeads)
    {
        if (State != MatchState.CoinToss) return;
        bool heads = UnityEngine.Random.value < 0.5f;
        bool playerWins = heads == playerPicksHeads;
        firstServer = playerWins ? playerTeam : Other(playerTeam);

        string chosen = playerPicksHeads ? "CARA" : "CREU";
        string result = heads ? "CARA" : "CREU";
        coinResultText = $"Has triat {chosen} · Ha sortit {result}";
        coinServeText = playerWins ? "Treus tu!" : "Treu el rival";

        State = MatchState.CoinResult;
        if (CoinTossHandledExternally) CoinTossed?.Invoke(heads, playerWins);
        else StartCoroutine(CoinResultRoutine());
    }

    // Lo llama la pantalla del sorteo cuando ha terminado de enseñar el resultado
    public void CompleteCoinToss()
    {
        if (State == MatchState.CoinResult) StartKickoff(firstServer);
    }

    IEnumerator CoinResultRoutine()
    {
        yield return new WaitForSeconds(coinResultTime);
        StartKickoff(firstServer);
    }

    // ---------- Saque de centro ----------

    void StartKickoff(Team servingTeam)
    {
        ResetField();   // bola al centro y jugadores en formación

        kickoffTeam = servingTeam;
        TeamManager tm = servingTeam == Team.Home ? home : away;
        kickoffServer = PickServer(tm);

        // El sacador se coloca mirando a la portería rival, de modo que la bola quede justo en el centro
        Vector2 dir = ((Vector2)tm.opponentGoal.position - (Vector2)tm.ownGoal.position).normalized;
        kickoffServer.AimDir = dir;
        kickoffServer.TeleportTo(ballStart - dir * kickoffServer.holdDistance);
        kickoffServer.TakeBall(ball);

        State = MatchState.Kickoff;
    }

    // Saca el jugador cuya posición de formación está más cerca del centro
    Skater PickServer(TeamManager tm)
    {
        Skater best = null;
        float bestDist = float.MaxValue;
        foreach (Skater s in tm.skaters)
        {
            float d = Vector2.Distance(s.Anchor, ballStart);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    // ---------- Goles ----------

    public void GoalScored(Team scorer)
    {
        if (State != MatchState.Playing) return;   // sin tiempo en juego no hay gol
        State = MatchState.GoalPause;              // reloj parado y jugadores quietos

        if (scorer == Team.Home) homeScore++;
        else awayScore++;

        // Gol de oro: el primer gol termina el partido (tras ver la celebración)
        if (IsGoldenGoal)
        {
            StartCoroutine(GoldenGoalEndRoutine());
            return;
        }

        // Saca el equipo que ha encajado el gol
        StartCoroutine(GoalRestartRoutine(Other(scorer)));
    }

    IEnumerator GoalRestartRoutine(Team servingTeam)
    {
        yield return new WaitForSeconds(resetDelay);
        StartKickoff(servingTeam);
    }

    IEnumerator GoldenGoalEndRoutine()
    {
        yield return new WaitForSeconds(resetDelay);
        FinishMatch();
    }

    // ---------- Partes ----------

    void EndOfHalf()
    {
        if (Half == 1)
        {
            State = MatchState.HalfTime;
            StartCoroutine(HalfTimeRoutine());
        }
        else if (requireWinner && homeScore == awayScore)
        {
            State = MatchState.OvertimeBreak;
            StartCoroutine(OvertimeRoutine());
        }
        else
        {
            FinishMatch();
        }
    }

    IEnumerator HalfTimeRoutine()
    {
        yield return new WaitForSeconds(halfTimeBreak);

        SwapSides();
        Half = 2;
        timeLeft = halfDuration;
        StartKickoff(Other(firstServer));   // en la 2ª parte saca el equipo contrario
    }

    IEnumerator OvertimeRoutine()
    {
        yield return new WaitForSeconds(overtimeBreak);

        Half = 3;
        IsGoldenGoal = true;
        StartKickoff(firstServer);          // la prórroga la saca quien sacó al principio
    }

    void FinishMatch()
    {
        if (State == MatchState.FullTime) return;
        State = MatchState.FullTime;
        if (MatchFinished != null) MatchFinished(homeScore, awayScore);
    }

    void SwapSides()
    {
        home.SwapSides();
        away.SwapSides();

        // Cada portería pasa a ser defendida por el otro equipo
        foreach (Goal g in goals) g.SwapDefendingTeam();
    }

    void ResetField()
    {
        ball.ResetTo(ballStart);
        home.ResetPositions();
        away.ResetPositions();
    }

    static Team Other(Team t) => t == Team.Home ? Team.Away : Team.Home;

    // Red de seguridad: si la bola suelta acaba fuera de la pista, vuelve al centro
    void CheckBallInBounds()
    {
        if (rinkWalls == null || ball.IsHeld) return;

        Bounds b = rinkWalls.bounds;
        Vector2 p = ball.rb.position;
        if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y)
            ball.ResetTo(ballStart);
    }

    // ---------- Marcador y mensajes provisionales (OnGUI) ----------

    void OnGUI()
    {
        if (PauseController.IsPaused || HudVisibility.Hidden) return;
        GUIScale.Apply();   // HUD medido a 1920x1080: se escala a cualquier pantalla (movil, tableta...)

        // Con el marcador nuevo (ScoreboardHUD) aqui solo queda el sorteo antiguo, por si no hay pantalla de moneda
        if (scoreboard != null)
        {
            if (State == MatchState.CoinToss && !CoinTossHandledExternally) DrawCoinToss();
            else if (State == MatchState.CoinResult && !CoinTossHandledExternally) DrawCoinResult();
            return;
        }

        GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(10, 10, 700, 40), $"{homeName} {homeScore} - {awayScore} {awayName}", hud);
        string clock = IsGoldenGoal ? "PRÒRROGA · GOL D'OR" : $"{Half}a PART   {FormatTime(timeLeft)}";
        GUI.Label(new Rect(10, 50, 700, 40), clock, hud);

        switch (State)
        {
            case MatchState.CoinToss: if (!CoinTossHandledExternally) DrawCoinToss(); break;
            case MatchState.CoinResult: if (!CoinTossHandledExternally) DrawCoinResult(); break;
            case MatchState.Kickoff: DrawKickoffHint(); break;
            case MatchState.GoalPause: DrawGoalScreen(); break;
            case MatchState.HalfTime: DrawCentered("DESCANS", 80, 0f); break;
            case MatchState.OvertimeBreak: DrawOvertimeScreen(); break;
            case MatchState.FullTime: DrawFullTimeScreen(); break;
        }
    }

    void DrawCoinToss()
    {
        DrawDim(0.6f);
        DrawCentered("SORTEIG", 70, -160f);
        DrawCentered("Tria cara o creu", 40, -90f);

        GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
        float w = 220f, h = 80f, y = GUIScale.Height * 0.5f - 10f, cx = GUIScale.Width * 0.5f;

        // GUIScale.Button = GUI.Button que tambien responde a toques (movil / tableta)
        if (GUIScale.Button(new Rect(cx - w - 20f, y, w, h), "CARA", btn)) ChooseCoin(true);
        if (GUIScale.Button(new Rect(cx + 20f, y, w, h), "CREU", btn)) ChooseCoin(false);
    }

    void DrawCoinResult()
    {
        DrawDim(0.6f);
        DrawCentered(coinResultText, 50, -50f);
        DrawCentered(coinServeText, 90, 60f);
    }

    void DrawKickoffHint()
    {
        string how = InputMode.IsTouch ? "(botó PASSA)" : "(clic dret)";
        string text = kickoffTeam == playerTeam
            ? "TREUS TU: passa-la a un company " + how
            : "Treu el rival";

        DrawCentered(text, 36, 100f - GUIScale.Height * 0.5f);
    }

    void DrawGoalScreen()
    {
        DrawDim(0.45f);
        DrawCentered(IsGoldenGoal ? "GOL D'OR!" : "GOL!", 140, -80f);
        DrawCentered($"{homeScore} - {awayScore}", 90, 80f);
    }

    void DrawOvertimeScreen()
    {
        DrawDim(0.6f);
        DrawCentered("EMPAT", 70, -80f);
        DrawCentered("Pròrroga a gol d'or: qui marqui primer guanya", 36, 20f);
    }

    void DrawFullTimeScreen()
    {
        DrawDim(0.75f);

        string result = homeScore > awayScore ? "GUANYA " + homeName
                      : awayScore > homeScore ? "GUANYA " + awayName
                      : "EMPAT";

        DrawCentered("FINAL DEL PARTIT", 50, -140f);
        DrawCentered($"{homeScore} - {awayScore}", 140, 0f);
        DrawCentered(result, 50, 140f);
    }

    void DrawDim(float alpha)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, GUIScale.Width, GUIScale.Height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawCentered(string text, int fontSize, float yOffset)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;

        GUI.Label(new Rect(0, GUIScale.Height * 0.5f + yOffset - 100f, GUIScale.Width, 200f), text, style);
    }

    string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);   // 03:00 al empezar, 00:00 al terminar
        return $"{s / 60:00}:{s % 60:00}";
    }
}