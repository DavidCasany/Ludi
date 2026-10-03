using System.Collections;
using UnityEngine;

public enum MatchState
{
    Playing,    // el reloj corre
    GoalPause,  // gol: reloj parado hasta reiniciar
    HalfTime,   // fin de la 1ª parte: reloj parado, jugadores quietos
    FullTime    // fin del partido
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public EdgeCollider2D rinkWalls;

    [Header("Equipos y bola")]
    public TeamManager home;
    public TeamManager away;
    public Ball ball;
    public Vector2 ballStart = Vector2.zero;

    [Header("Tiempo de partido")]
    public float halfDuration = 20f;   // 3 minutos por parte
    public float resetDelay = 2f;       // pausa tras un gol
    public float halfTimeBreak = 4f;    // pausa del descanso

    [Header("Marcador")]
    public int homeScore;
    public int awayScore;

    public MatchState State { get; private set; } = MatchState.Playing;
    public int Half { get; private set; } = 1;

    // Skater lo consulta para quedarse quieto en el descanso y al acabar el partido
    public bool PlayersFrozen => State == MatchState.HalfTime || State == MatchState.FullTime;

    float timeLeft;
    Goal[] goals;

    void Awake()
    {
        Instance = this;
        goals = FindObjectsByType<Goal>(FindObjectsSortMode.None);
        timeLeft = halfDuration;
    }

    void Update()
    {
        CheckBallInBounds();
        // El reloj solo corre mientras se juega
        if (State != MatchState.Playing) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            EndOfHalf();
        }
    }

    // ---------- Goles ----------

    public void GoalScored(Team scorer)
    {
        if (State != MatchState.Playing) return;   // sin tiempo en juego no hay gol
        State = MatchState.GoalPause;              // el reloj se detiene

        if (scorer == Team.Home) homeScore++;
        else awayScore++;

        StartCoroutine(GoalRestartRoutine());
    }

    IEnumerator GoalRestartRoutine()
    {
        yield return new WaitForSeconds(resetDelay);

        ResetField();
        State = MatchState.Playing;                // el reloj vuelve a correr
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

        SwapSides();      // primero se cambian los lados...
        ResetField();     // ...y después se colocan bola y jugadores
        Half = 2;
        timeLeft = halfDuration;
        State = MatchState.Playing;
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

    // ---------- Marcador provisional (OnGUI) ----------

    void OnGUI()
    {
        GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(10, 10, 500, 40), $"HOME {homeScore} - {awayScore} AWAY", hud);
        GUI.Label(new Rect(10, 50, 500, 40), $"{Half}ª PARTE   {FormatTime(timeLeft)}", hud);

        if (State == MatchState.HalfTime) DrawCentered("DESCANSO", 80, 0f);
        else if (State == MatchState.FullTime) DrawFullTimeScreen();
    }

    void DrawFullTimeScreen()
    {
        // Fondo oscuro
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string result = homeScore > awayScore ? "GANA HOME"
                      : awayScore > homeScore ? "GANA AWAY"
                      : "EMPATE";

        DrawCentered("FIN DEL PARTIDO", 50, -140f);
        DrawCentered($"{homeScore} - {awayScore}", 140, 0f);
        DrawCentered(result, 50, 140f);
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
    void CheckBallInBounds()
    {
        if (rinkWalls == null || ball.IsHeld) return;

        Bounds b = rinkWalls.bounds;
        Vector2 p = ball.rb.position;
        if (p.x < b.min.x || p.x > b.max.x || p.y < b.min.y || p.y > b.max.y)
            ball.ResetTo(ballStart);
    }
}
