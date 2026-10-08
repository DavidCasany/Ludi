using System.Collections.Generic;
using UnityEngine;

public enum AIRole { Carrier, Hunter, Finisher, Winger, Safety, Cover, Marker }

public struct AIAssignment
{
    public AIRole role;
    public Vector2 target;
    public AIAssignment(AIRole role, Vector2 target) { this.role = role; this.target = target; }
}

// Cerebro del equipo. Se añade solo al GameObject del TeamManager (lo crea SkaterAI si no existe).
// Cada ~0.15 s decide qué ROL hace cada jugador y a qué POSICIÓN debe ir, de forma coordinada:
//
//  Con bola (ataque):  Carrier (con bola) · Finisher (delante de la portería) · Winger (ancho) · Safety (cobertura atrás)
//  Sin bola (defensa): Hunter (presiona la bola) · Cover (tapa el tiro) · Marker x2 (marcan a los rivales más peligrosos)
//  Bola suelta:        Hunter (el que llega antes) + posturas de ataque o defensa según quién la tocó último
//
// Los roles se reparten probando todas las combinaciones y eligiendo la que minimiza el recorrido total,
// con "pegajosidad" para que los jugadores no cambien de rol constantemente.
[RequireComponent(typeof(TeamManager))]
public class TeamBrain : MonoBehaviour
{
    [Header("Escala")]
    [Tooltip("Largo de pista de referencia (m). Todas las distancias de la IA se escalan con (largo real de la pista / este valor).")]
    public float referenceRinkLength = 40f;

    [Header("Reparto de roles")]
    public float updateInterval = 0.15f;
    public float roleStickiness = 2f;      // metros de ventaja por mantener el rol actual
    public float hunterStickiness = 0.8f;  // el cazador actual solo cambia si otro llega un 20% antes

    [Header("Geometría")]
    public float goalHalfWidth = 0.85f;    // mitad del ancho de la portería
    public float goalAvoidRadius = 2.8f;   // los jugadores no se colocan dentro de esta zona
    public float rinkMargin = 1.2f;
    public Vector2 fallbackRinkHalfSize = new Vector2(19f, 9f); // solo si GameManager.rinkWalls no está asignado

    [Header("Debug")]
    public bool drawGizmos = true;

    TeamManager tm;
    readonly Dictionary<Skater, AIAssignment> assignments = new Dictionary<Skater, AIAssignment>();
    readonly Dictionary<Skater, AIRole> lastRoles = new Dictionary<Skater, AIRole>();
    readonly List<Skater> free = new List<Skater>(8);
    readonly List<AIAssignment> slots = new List<AIAssignment>(8);
    readonly List<Vector2> taken = new List<Vector2>(16);

    Skater currentHunter;
    Skater lastHolder;
    float nextUpdate;
    Vector2 rinkMin, rinkMax;
    bool rinkReady;

    public float Unit { get; private set; } = 1f;
    public float M(float meters) => meters * Unit;

    // ---------- Geometría del campo ----------

    Vector2 OwnGoalPos => tm.ownGoal.position;
    Vector2 OppGoalPos => tm.opponentGoal.position;
    public Vector2 Center => (OwnGoalPos + OppGoalPos) * 0.5f;
    public Vector2 AttackDir => (OppGoalPos - OwnGoalPos).normalized;
    public Vector2 Lateral { get { Vector2 a = AttackDir; return new Vector2(-a.y, a.x); } }

    void Awake()
    {
        tm = GetComponent<TeamManager>();
        rinkMin = -fallbackRinkHalfSize;
        rinkMax = fallbackRinkHalfSize;
    }

    void EnsureRink()
    {
        if (rinkReady) return;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.rinkWalls == null) return;   // seguimos con el valor por defecto

        Bounds b = gm.rinkWalls.bounds;
        rinkMin = b.min;
        rinkMax = b.max;
        Unit = Mathf.Max(0.1f, b.size.x / referenceRinkLength);
        rinkReady = true;
    }

    public bool IsInsideRink(Vector2 p, float margin)
    {
        return p.x > rinkMin.x + margin && p.x < rinkMax.x - margin &&
               p.y > rinkMin.y + margin && p.y < rinkMax.y - margin;
    }

    Vector2 ClampToRink(Vector2 p)
    {
        float m = M(rinkMargin);
        return new Vector2(Mathf.Clamp(p.x, rinkMin.x + m, rinkMax.x - m),
                           Mathf.Clamp(p.y, rinkMin.y + m, rinkMax.y - m));
    }

    Vector2 PushOutOfGoal(Vector2 p, Vector2 goal)
    {
        float r = M(goalAvoidRadius);
        Vector2 d = p - goal;
        if (d.magnitude >= r) return p;

        Vector2 inward = (Center - goal).normalized;
        Vector2 dir = d.sqrMagnitude < 0.0001f ? inward : d.normalized;
        if (Vector2.Dot(dir, inward) < 0.3f) dir = (dir + inward * 1.2f).normalized;
        return goal + dir * r;
    }

    Vector2 Sanitize(Vector2 p)
    {
        p = ClampToRink(p);
        p = PushOutOfGoal(p, OwnGoalPos);
        p = PushOutOfGoal(p, OppGoalPos);
        return p;
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return Vector2.Distance(p, a + ab * t);
    }

    // ---------- Evaluación (la usa también SkaterAI) ----------

    // 1 = línea libre, 0 = un rival la bloquea
    public float LaneClear(Vector2 a, Vector2 b)
    {
        if (tm.opponent == null) return 1f;
        float worst = 1f;
        foreach (Skater o in tm.opponent.skaters)
        {
            float d = DistToSegment(o.transform.position, a, b);
            worst = Mathf.Min(worst, Mathf.Clamp01((d - M(0.5f)) / M(1.2f)));
        }
        return worst;
    }

    // Calidad de tiro desde 'from' (0..1) y punto al que apuntar.
    // Prueba las dos esquinas de la portería y devuelve la mejor.
    // Una línea tapada resta calidad pero no la anula (los tiros con tráfico también acaban en rechace o gol).
    public float ShotQuality(Vector2 from, out Vector2 aimPoint)
    {
        Vector2 goal = OppGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;
        aimPoint = goal;

        float along = Vector2.Dot(goal - from, atk);
        if (along < M(0.8f)) return 0f;                       // por detrás de la línea de gol

        float d = Vector2.Distance(from, goal);
        float distF = 1f - Mathf.Clamp01((d - M(3f)) / M(9f)); // 1 a <=3 m, 0 a >=12 m
        float angleF = Mathf.Clamp01((along / d - 0.3f) / 0.6f); // los ángulos cerrados valen poco

        float keeperLat = 0f;
        Goalkeeper gk = tm.opponent != null ? tm.opponent.goalkeeper : null;
        if (gk != null) keeperLat = Vector2.Dot((Vector2)gk.transform.position - goal, lat);

        float best = 0f;
        for (int side = -1; side <= 1; side += 2)
        {
            float aimLat = side * goalHalfWidth * 0.75f;
            Vector2 p = goal + lat * aimLat;

            float laneF = 0.3f + 0.7f * LaneClear(from, p);
            float openF = 0.5f + 0.5f * Mathf.Clamp01(Mathf.Abs(aimLat - keeperLat) / M(1f));
            float q = distF * angleF * laneF * openF;

            if (q >= best) { best = q; aimPoint = p; }
        }
        return best;
    }

    public bool TryGet(Skater s, out AIAssignment a) => assignments.TryGetValue(s, out a);

    // ---------- Bucle principal ----------

    void Update()
    {
        Ball ball = Ball.Instance;
        if (ball == null || tm.ownGoal == null || tm.opponentGoal == null) return;

        EnsureRink();

        bool possessionChanged = ball.Holder != lastHolder;
        if (!possessionChanged && Time.time < nextUpdate) return;

        lastHolder = ball.Holder;
        nextUpdate = Time.time + updateInterval;
        Recompute(ball);
    }

    void Recompute(Ball ball)
    {
        assignments.Clear();
        taken.Clear();
        free.Clear();

        // El jugador que controla el humano no se asigna, pero ocupa espacio
        foreach (Skater s in tm.skaters)
        {
            if (s.IsControlled) taken.Add(s.transform.position);
            else free.Add(s);
        }

        Vector2 ballPos = ball.rb.position;
        bool ours = ball.IsHeld && ball.Holder.team == tm.team;
        bool attackingPosture = ours || (!ball.IsHeld && ball.LastTouchTeam == tm.team);

        // 1) Portador
        if (ours)
        {
            taken.Add(ballPos);
            if (free.Remove(ball.Holder))
                assignments[ball.Holder] = new AIAssignment(AIRole.Carrier, ballPos);
            currentHunter = null;
        }
        else
        {
            // 2) Cazador de la bola
            Skater hunter = PickHunter(ball);
            if (hunter != null)
            {
                assignments[hunter] = new AIAssignment(AIRole.Hunter, InterceptPoint(hunter, ball));
                free.Remove(hunter);
            }
            currentHunter = hunter;
        }

        // 3) Posiciones para el resto
        slots.Clear();
        Vector2 refPos = ball.IsHeld ? (Vector2)ball.Holder.transform.position : ballPos;
        if (attackingPosture) BuildAttackSlots(refPos, slots);
        else BuildDefenseSlots(ballPos, ball, slots);

        while (slots.Count > free.Count) slots.RemoveAt(slots.Count - 1);
        while (slots.Count < free.Count)
            slots.Add(new AIAssignment(AIRole.Safety, Sanitize(Vector2.Lerp(OwnGoalPos, Center, 0.4f))));

        Assign(free, slots);

        foreach (KeyValuePair<Skater, AIAssignment> kv in assignments)
            lastRoles[kv.Key] = kv.Value.role;
    }

    // ---------- Cazador ----------

    Vector2 InterceptPoint(Skater s, Ball ball)
    {
        Vector2 p = ball.rb.position;
        if (ball.IsHeld) return ClampToRink(p + ball.Holder.Velocity * 0.3f);

        Vector2 v = ball.rb.linearVelocity;
        float damp = Mathf.Max(ball.rb.linearDamping, 0.01f);
        Vector2 target = p;
        for (int i = 0; i < 3; i++)
        {
            float t = Mathf.Min(Vector2.Distance(s.transform.position, target) / s.maxSpeed, 1.5f);
            float travel = (1f - Mathf.Exp(-damp * t)) / damp;   // distancia recorrida con rozamiento
            target = p + v * travel;
        }
        return ClampToRink(target);
    }

    Skater PickHunter(Ball ball)
    {
        Skater best = null;
        float bestT = float.MaxValue;
        foreach (Skater s in free)
        {
            float t = Vector2.Distance(s.transform.position, InterceptPoint(s, ball)) / s.maxSpeed;
            if (s == currentHunter) t *= hunterStickiness;
            if (t < bestT) { bestT = t; best = s; }
        }

        // Bola suelta y el jugador humano llega claramente antes: no hace falta cazador
        if (best != null && !ball.IsHeld)
        {
            foreach (Skater s in tm.skaters)
            {
                if (!s.IsControlled) continue;
                float th = Vector2.Distance(s.transform.position, InterceptPoint(s, ball)) / s.maxSpeed;
                if (th < bestT * 0.8f) return null;
            }
        }
        return best;
    }

    // ---------- Posiciones de ataque ----------

    void BuildAttackSlots(Vector2 carrierPos, List<AIAssignment> list)
    {
        Vector2 goal = OppGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;

        float lc = Vector2.Dot(carrierPos - Center, lat);
        float cs = lc >= 0f ? 1f : -1f;                               // lado en el que está el portador
        float cd = Mathf.Max(Vector2.Dot(goal - carrierPos, atk), 0f); // distancia a portería sobre el eje

        // Rematador: delante de la portería, al lado contrario del portador (poste lejano)
        float fd = Mathf.Clamp(cd - M(8f), M(4.5f), M(9f));
        Vector2 finisher = goal - atk * fd + lat * (-cs * M(1.8f));

        // Extremo: abierto por el lado contrario, algo por delante de la bola
        float wd = Mathf.Max(cd - M(2.5f), M(6f));
        Vector2 winger = goal - atk * wd + lat * (-cs * M(6f));

        // Seguridad: detrás del balón, ofrece pase atrás y cubre el contraataque
        float sd = Mathf.Min(cd + M(6f), M(26f));
        Vector2 safety = goal - atk * sd + lat * (lc * 0.3f);

        list.Add(new AIAssignment(AIRole.Finisher, Refine(finisher, carrierPos, M(1.8f), true)));
        list.Add(new AIAssignment(AIRole.Winger, Refine(winger, carrierPos, M(2f), false)));
        list.Add(new AIAssignment(AIRole.Safety, Refine(safety, carrierPos, M(2f), false)));
    }

    // Busca, alrededor de una posición base, el punto más libre: lejos de rivales,
    // con línea de pase limpia desde el portador, separado de compañeros y (si toca) con buen tiro.
    Vector2 Refine(Vector2 center, Vector2 from, float radius, bool wantShot)
    {
        center = Sanitize(center);
        Vector2 best = center;
        float bestScore = float.MinValue;

        for (int k = 0; k < 9; k++)
        {
            Vector2 p = center;
            if (k > 0)
            {
                float ang = (k - 1) * 45f * Mathf.Deg2Rad;
                p += new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            }
            p = Sanitize(p);

            float open = Mathf.Clamp01(tm.DistanceToNearestOpponent(p) / M(5f));
            float lane = LaneClear(from, p);
            float score = open + 1.5f * lane - 0.6f * Vector2.Distance(p, center) / radius;

            if (wantShot)
            {
                Vector2 aim;
                score += 1.5f * ShotQuality(p, out aim);
            }

            foreach (Vector2 t in taken)
            {
                float d = Vector2.Distance(p, t);
                if (d < M(3f)) score -= 1.2f * (1f - d / M(3f));
            }

            if (score > bestScore) { bestScore = score; best = p; }
        }

        taken.Add(best);
        return best;
    }

    // ---------- Posiciones de defensa ----------

    void BuildDefenseSlots(Vector2 ballPos, Ball ball, List<AIAssignment> list)
    {
        Vector2 own = OwnGoalPos;
        Vector2 carrier = ball.IsHeld ? (Vector2)ball.Holder.transform.position : ballPos;

        // Cobertura: entre la bola y nuestra portería, tapando la línea de tiro
        Vector2 toGoal = own - carrier;
        toGoal = toGoal.sqrMagnitude < 0.01f ? -AttackDir : toGoal.normalized;
        float coverDist = Mathf.Min(M(4.5f), Vector2.Distance(carrier, own) * 0.5f);
        Vector2 cover = Sanitize(carrier + toGoal * coverDist);
        list.Add(new AIAssignment(AIRole.Cover, cover));
        taken.Add(cover);

        if (tm.opponent == null) return;

        // Marcajes: los dos rivales más peligrosos (cerca de nuestra portería y con línea de pase libre)
        Skater ballOpp = ball.IsHeld ? ball.Holder : NearestOpponentTo(ballPos);
        Skater m1 = null, m2 = null;
        float s1 = float.MinValue, s2 = float.MinValue;

        foreach (Skater o in tm.opponent.skaters)
        {
            if (o == ballOpp) continue;
            Vector2 op = o.transform.position;
            float danger = (1f - Mathf.Clamp01(Vector2.Distance(op, own) / M(30f))) + LaneClear(carrier, op);

            if (danger > s1) { s2 = s1; m2 = m1; s1 = danger; m1 = o; }
            else if (danger > s2) { s2 = danger; m2 = o; }
        }

        if (m1 != null) list.Add(MarkSlot(m1, own, carrier));
        if (m2 != null) list.Add(MarkSlot(m2, own, carrier));
    }

    AIAssignment MarkSlot(Skater o, Vector2 own, Vector2 carrier)
    {
        Vector2 op = o.transform.position;
        Vector2 goalSide = (own - op).normalized;
        Vector2 ballSide = (carrier - op).normalized;
        Vector2 dir = (goalSide * 0.7f + ballSide * 0.3f).normalized;   // por el lado de la portería, algo hacia el pase
        return new AIAssignment(AIRole.Marker, Sanitize(op + dir * M(1.4f)));
    }

    Skater NearestOpponentTo(Vector2 p)
    {
        Skater best = null;
        float bestD = float.MaxValue;
        if (tm.opponent == null) return null;
        foreach (Skater o in tm.opponent.skaters)
        {
            float d = Vector2.Distance(o.transform.position, p);
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    // ---------- Reparto óptimo jugador <-> posición ----------

    float Cost(Skater s, AIAssignment slot)
    {
        float c = Vector2.Distance(s.transform.position, slot.target);
        AIRole prev;
        if (lastRoles.TryGetValue(s, out prev) && prev == slot.role) c -= M(roleStickiness);
        return c;
    }

    void Assign(List<Skater> players, List<AIAssignment> slotList)
    {
        int n = players.Count;
        if (n == 0) return;

        int[] perm = new int[n];
        int[] best = new int[n];
        bool[] used = new bool[slotList.Count];
        float bestCost = float.MaxValue;

        Search(0, 0f, players, slotList, perm, best, used, ref bestCost);

        for (int i = 0; i < n; i++) assignments[players[i]] = slotList[best[i]];
    }

    void Search(int i, float cost, List<Skater> players, List<AIAssignment> slotList,
                int[] perm, int[] best, bool[] used, ref float bestCost)
    {
        if (cost >= bestCost) return;
        if (i == players.Count)
        {
            bestCost = cost;
            System.Array.Copy(perm, best, perm.Length);
            return;
        }

        for (int j = 0; j < slotList.Count; j++)
        {
            if (used[j]) continue;
            used[j] = true;
            perm[i] = j;
            Search(i + 1, cost + Cost(players[i], slotList[j]), players, slotList, perm, best, used, ref bestCost);
            used[j] = false;
        }
    }

    // ---------- Debug ----------

    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying) return;

        foreach (KeyValuePair<Skater, AIAssignment> kv in assignments)
        {
            if (kv.Key == null) continue;
            Gizmos.color = RoleColor(kv.Value.role);
            Gizmos.DrawLine(kv.Key.transform.position, kv.Value.target);
            Gizmos.DrawWireSphere(kv.Value.target, 0.4f);
        }
    }

    static Color RoleColor(AIRole r)
    {
        switch (r)
        {
            case AIRole.Carrier: return Color.white;
            case AIRole.Hunter: return Color.red;
            case AIRole.Finisher: return Color.yellow;
            case AIRole.Winger: return Color.cyan;
            case AIRole.Safety: return Color.blue;
            case AIRole.Cover: return Color.magenta;
            default: return new Color(1f, 0.5f, 0f);
        }
    }
}