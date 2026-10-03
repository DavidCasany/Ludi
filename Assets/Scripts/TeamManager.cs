using System.Collections.Generic;
using UnityEngine;

public enum Team { Home, Away }

// Pon este script en un GameObject padre y cuelga de él los 4 Skaters y el Goalkeeper.
public class TeamManager : MonoBehaviour
{
    public Team team;
    public bool humanControlled = true;
    public TeamManager opponent;
    public Transform ownGoal;        // centro de la portería que defiende
    public Transform opponentGoal;   // centro de la portería rival

    public List<Skater> skaters = new List<Skater>();  // jugadores de campo (se autorrellena)
    public Goalkeeper goalkeeper;                      // se autorrellena

    public Skater Controlled { get; private set; }

    Camera cam;
    float nextAutoSwitch;

    void Awake()
    {
        cam = Camera.main;
        if (skaters.Count == 0) skaters.AddRange(GetComponentsInChildren<Skater>());
        if (goalkeeper == null) goalkeeper = GetComponentInChildren<Goalkeeper>();

        foreach (Skater s in skaters) s.team = team;
        if (goalkeeper != null) goalkeeper.team = team;
    }

    void Start()
    {
        if (humanControlled && skaters.Count > 0) SetControlled(skaters[0]);
    }

    void Update()
    {
        if (!humanControlled || Controlled == null) return;

        AutoSelect();
        HandleInput();
    }

    // ---------- Input del humano ----------

    void HandleInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Controlled.MoveInput = Vector2.ClampMagnitude(input, 1f);

        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mouse - (Vector2)Controlled.transform.position;
        if (toMouse.sqrMagnitude > 0.001f) Controlled.AimDir = toMouse.normalized;

        if (Input.GetMouseButtonDown(0))   // chutar
            Controlled.Shoot(Controlled.AimDir, Controlled.shootForce);

        if (Input.GetMouseButtonDown(1))   // pase al compañero hacia donde apunta el ratón
            Controlled.PassTo(BestPassTarget(Controlled));

        if (Input.GetKeyDown(KeyCode.Space)) // cambiar al jugador más cercano a la bola
            SwitchToClosestToBall();
    }

    // ---------- Cambio de jugador ----------

    void AutoSelect()
    {
        Ball ball = Ball.Instance;
        if (ball == null) return;

        // Siempre controlas a quien lleva la bola
        if (ball.IsHeld && ball.Holder.team == team)
        {
            if (ball.Holder != Controlled) SetControlled(ball.Holder);
            return;
        }

        // Bola suelta tras un pase/tiro nuestro: pasar al que va a recibirla
        if (!ball.IsHeld && ball.LastTouchTeam == team && Time.time >= nextAutoSwitch)
        {
            Skater nearest = ClosestToBall();
            float dNew = Vector2.Distance(nearest.transform.position, ball.transform.position);
            float dCur = Vector2.Distance(Controlled.transform.position, ball.transform.position);
            if (nearest != Controlled && dNew + 1f < dCur)
            {
                SetControlled(nearest);
                nextAutoSwitch = Time.time + 0.3f;
            }
        }
    }

    void SwitchToClosestToBall()
    {
        Ball ball = Ball.Instance;
        Skater best = null;
        float bestDist = float.MaxValue;
        foreach (Skater s in skaters)
        {
            if (s == Controlled) continue;
            float d = Vector2.Distance(s.transform.position, ball.transform.position);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) SetControlled(best);
    }

    public void SetControlled(Skater s)
    {
        if (Controlled != null) Controlled.SetControlled(false);
        Controlled = s;
        Controlled.SetControlled(true);
    }

    // ---------- Utilidades (las usa también la IA) ----------

    public Skater ClosestToBall()
    {
        Ball ball = Ball.Instance;
        Skater best = null;
        float bestDist = float.MaxValue;
        foreach (Skater s in skaters)
        {
            float d = Vector2.Distance(s.transform.position, ball.transform.position);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    public float DistanceToNearestOpponent(Vector2 pos)
    {
        float best = float.MaxValue;
        if (opponent == null) return best;
        foreach (Skater s in opponent.skaters)
            best = Mathf.Min(best, Vector2.Distance(pos, s.transform.position));
        return best;
    }

    // Compañero más alineado con la dirección de apuntado (y no demasiado lejos)
    Skater BestPassTarget(Skater from)
    {
        Skater best = null;
        float bestScore = 0.5f; // mínimo de alineación (coseno)
        foreach (Skater s in skaters)
        {
            if (s == from) continue;
            Vector2 to = s.transform.position - from.transform.position;
            float score = Vector2.Dot(from.AimDir, to.normalized) - to.magnitude * 0.01f;
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    public void ResetPositions()
    {
        foreach (Skater s in skaters) s.ResetToAnchor();
        if (goalkeeper != null) goalkeeper.ResetPosition();
    }

    public void SwapSides()
    {
        (ownGoal, opponentGoal) = (opponentGoal, ownGoal);

        foreach (Skater s in skaters) s.MirrorAnchor();
        if (goalkeeper != null) goalkeeper.SwapSide();
    }
}
