using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public TeamManager home;
    public TeamManager away;
    public Ball ball;
    public Vector2 ballStart = Vector2.zero;
    public float resetDelay = 2f;

    public int homeScore;
    public int awayScore;

    bool resetting;

    void Awake()
    {
        Instance = this;
    }

    public void GoalScored(Team scorer)
    {
        if (resetting) return;
        resetting = true;

        if (scorer == Team.Home) homeScore++;
        else awayScore++;

        StartCoroutine(ResetRoutine());
    }

    IEnumerator ResetRoutine()
    {
        yield return new WaitForSeconds(resetDelay);

        ball.ResetTo(ballStart);
        home.ResetPositions();
        away.ResetPositions();

        resetting = false;
    }

    // Marcador provisional para el prototipo
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        GUI.Label(new Rect(10, 10, 400, 40), $"HOME {homeScore} - {awayScore} AWAY", style);
    }
}
