using UnityEngine;

// IA sencilla basada en 3 roles dinámicos:
//  - Con la bola: conduce hacia la portería, pasa si le presionan, dispara en rango.
//  - El más cercano a la bola (si no la tiene su equipo): va a por ella.
//  - El resto: se coloca en su posición de formación, siguiendo a la bola.
[RequireComponent(typeof(Skater))]
public class SkaterAI : MonoBehaviour
{
    [Header("Ataque")]
    public float shootRange = 9f;
    public float goalHalfWidth = 0.85f;        // mitad del ancho de la portería
    public float passPressureDistance = 2.5f;  // si un rival está más cerca, intenta pasar
    public float thinkTime = 0.35f;            // pausa antes de decidir tras recibir la bola

    [Header("Posicionamiento")]
    public float formationShift = 0.4f;        // cuánto sigue el equipo a la bola
    public float attackBias = 3f;              // cuánto se adelanta al atacar
    public Vector2 rinkHalfSize = new Vector2(19f, 9f);

    Skater me;
    TeamManager tm;
    bool hadBall;
    float ballSince;

    public float serveDelay = 3f;
    Skater NearestTeammate()
    {
        Skater best = null;
        float bestDist = float.MaxValue;
        foreach (Skater s in tm.skaters)
        {
            if (s == me) continue;
            float d = Vector2.Distance(s.transform.position, transform.position);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    void Awake()
    {
        me = GetComponent<Skater>();
        tm = GetComponentInParent<TeamManager>();
    }

    void FixedUpdate()
    {
        Ball ball = Ball.Instance;
        if (ball == null || tm == null) return;

        if (me.HasBall && GameManager.Instance != null && GameManager.Instance.IsServing(me))
        {
            if (Time.time - ballSince > serveDelay) me.PassTo(NearestTeammate());
            return;
        }

        if (me.HasBall && !hadBall) ballSince = Time.time;
        hadBall = me.HasBall;

        if (me.HasBall)
        {
            Attack();
            return;
        }

        bool teamHasBall = ball.IsHeld && ball.Holder.team == me.team;
        Vector2 ballPos = ball.transform.position;

        if (!teamHasBall && tm.ClosestToBall() == me)
        {
            // Ir a por la bola (anticipando su movimiento)
            Vector2 predicted = ballPos + ball.rb.linearVelocity * 0.25f;
            GoTo(predicted, 0.01f);
        }
        else
        {
            GoTo(FormationPosition(ballPos, teamHasBall), 1.5f);
        }

        me.AimDir = (ballPos - (Vector2)transform.position).normalized;
    }

    void Attack()
    {
        Vector2 pos = transform.position;
        Vector2 goal = tm.opponentGoal.position;
        float distToGoal = Vector2.Distance(pos, goal);
        bool thinking = Time.time - ballSince < thinkTime;

        // 1) Disparo en rango, a un punto aleatorio de la portería
        if (!thinking && distToGoal <= shootRange)
        {
            Vector2 aimPoint = goal + new Vector2(0f, Random.Range(-goalHalfWidth, goalHalfWidth));
            me.Shoot(aimPoint - pos, me.shootForce);
            return;
        }

        // 2) Pase si hay un rival encima
        if (!thinking && tm.DistanceToNearestOpponent(pos) < passPressureDistance)
        {
            Skater mate = BestPassTarget(goal, distToGoal);
            if (mate != null)
            {
                me.PassTo(mate);
                return;
            }
        }

        // 3) Conducir hacia la portería
        me.AimDir = (goal - pos).normalized;
        GoTo(goal, 1.5f);
    }

    Skater BestPassTarget(Vector2 goal, float myDistToGoal)
    {
        Skater best = null;
        float bestScore = 1f; // el compañero debe estar al menos 1 m más cerca de la portería
        foreach (Skater s in tm.skaters)
        {
            if (s == me) continue;
            Vector2 sp = s.transform.position;
            if (tm.DistanceToNearestOpponent(sp) < 2f) continue; // está marcado

            float score = myDistToGoal - Vector2.Distance(sp, goal);
            if (score > bestScore) { bestScore = score; best = s; }
        }
        return best;
    }

    Vector2 FormationPosition(Vector2 ballPos, bool attacking)
    {
        Vector2 p = me.Anchor + ballPos * formationShift;

        if (attacking)
        {
            Vector2 attackDir = ((Vector2)tm.opponentGoal.position - (Vector2)tm.ownGoal.position).normalized;
            p += attackDir * attackBias;
        }

        p.x = Mathf.Clamp(p.x, -rinkHalfSize.x, rinkHalfSize.x);
        p.y = Mathf.Clamp(p.y, -rinkHalfSize.y, rinkHalfSize.y);
        return p;
    }

    // slowRadius: a esa distancia del objetivo empieza a frenar (0.01 = a tope hasta llegar)
    void GoTo(Vector2 target, float slowRadius)
    {
        Vector2 to = target - (Vector2)transform.position;
        float d = to.magnitude;
        me.MoveInput = d < 0.2f ? Vector2.zero : to.normalized * Mathf.Clamp01(d / slowRadius);
    }
}
