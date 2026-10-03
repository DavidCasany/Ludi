using System.Collections;
using UnityEngine;

public enum MatchState
{
    CoinToss,    // el jugador elige cara o cruz
    CoinResult,  // se muestra el resultado del sorteo
    Kickoff,     // saque de centro: reloj parado hasta el primer pase
    Playing,     // el reloj corre
    GoalPause,   // gol: reloj parado y jugadores quietos
    HalfTime,    // fin de la 1ª parte
    FullTime     // fin del partido
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
    public float coinResultTime = 2.5f; // cuánto se ve el resultado del sorteo

    [Header("Marcador")]
    public int homeScore;
    public int awayScore;

    public MatchState State { get; private set; } = MatchState.CoinToss;
    public int Half { get; private set; } = 1;

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
    }

    void Update()
    {
        CheckBallInBounds();

        // El reloj solo corre mientras se juega (no en el saque, ni en pausas)
        if (State != MatchState.Playing) return;

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

    void ChooseCoin(bool playerPicksHeads)
    {
        bool heads = Random.value < 0.5f;
        bool playerWins = heads == playerPicksHeads;
        firstServer = playerWins ? playerTeam : Other(playerTeam);

        string chosen = playerPicksHeads ? "CARA" : "CRUZ";
        string result = heads ? "CARA" : "CRUZ";
        coinResultText = $"Elegiste {chosen} · Ha salido {result}";
        coinServeText = playerWins ? "¡Sacas tú!" : "Saca el rival";

        State = MatchState.CoinResult;
        StartCoroutine(CoinResultRoutine());
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

        // Saca el equipo que ha encajado el gol
        StartCoroutine(GoalRestartRoutine(Other(scorer)));
    }

    IEnumerator GoalRestartRoutine(Team servingTeam)
    {
        yield return new WaitForSeconds(resetDelay);
        StartKickoff(servingTeam);
    }

    // ---------- Partes ----------

    void EndOfHalf()
    {
        if (Half == 1)
        {
            State = MatchState.HalfTime;
            StartCoroutine(HalfTimeRoutine());
        }
        else
        {
            State = MatchState.FullTime;
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
        GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(10, 10, 500, 40), $"HOME {homeScore} - {awayScore} AWAY", hud);
        GUI.Label(new Rect(10, 50, 500, 40), $"{Half}ª PARTE   {FormatTime(timeLeft)}", hud);

        switch (State)
        {
            case MatchState.CoinToss: DrawCoinToss(); break;
            case MatchState.CoinResult: DrawCoinResult(); break;
            case MatchState.Kickoff: DrawKickoffHint(); break;
            case MatchState.GoalPause: DrawGoalScreen(); break;
            case MatchState.HalfTime: DrawCentered("DESCANSO", 80, 0f); break;
            case MatchState.FullTime: DrawFullTimeScreen(); break;
        }
    }

    void DrawCoinToss()
    {
        DrawDim(0.6f);
        DrawCentered("SORTEO", 70, -160f);
        DrawCentered("Elige cara o cruz", 40, -90f);

        GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
        float w = 220f, h = 80f, y = Screen.height * 0.5f - 10f;

        if (GUI.Button(new Rect(Screen.width * 0.5f - w - 20f, y, w, h), "CARA", btn)) ChooseCoin(true);
        if (GUI.Button(new Rect(Screen.width * 0.5f + 20f, y, w, h), "CRUZ", btn)) ChooseCoin(false);
    }

    void DrawCoinResult()
    {
        DrawDim(0.6f);
        DrawCentered(coinResultText, 50, -50f);
        DrawCentered(coinServeText, 90, 60f);
    }

    void DrawKickoffHint()
    {
        string text = kickoffTeam == playerTeam
            ? "SAQUE: pasa a un compañero (clic derecho)"
            : "Saque del rival";

        DrawCentered(text, 36, 100f - Screen.height * 0.5f);
    }

    void DrawGoalScreen()
    {
        DrawDim(0.45f);
        DrawCentered("¡GOL!", 140, -80f);
        DrawCentered($"{homeScore} - {awayScore}", 90, 80f);
    }

    void DrawFullTimeScreen()
    {
        DrawDim(0.75f);

        string result = homeScore > awayScore ? "GANA HOME"
                      : awayScore > homeScore ? "GANA AWAY"
                      : "EMPATE";

        DrawCentered("FIN DEL PARTIDO", 50, -140f);
        DrawCentered($"{homeScore} - {awayScore}", 140, 0f);
        DrawCentered(result, 50, 140f);
    }

    void DrawDim(float alpha)
    {
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
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

        GUI.Label(new Rect(0, Screen.height * 0.5f + yOffset - 100f, Screen.width, 200f), text, style);
    }

    string FormatTime(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);   // 03:00 al empezar, 00:00 al terminar
        return $"{s / 60:00}:{s % 60:00}";
    }
}