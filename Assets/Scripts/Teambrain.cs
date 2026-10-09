using System.Collections.Generic;
using UnityEngine;

public enum AIRole { Carrier, Hunter, Finisher, Winger, Safety, Cover, Sweeper, Marker }

public struct AIAssignment
{
    public AIRole role;
    public Vector2 target;
    public AIAssignment(AIRole role, Vector2 target) { this.role = role; this.target = target; }
}

// Cerebro del equipo. Se añade solo al GameObject del TeamManager (lo crea SkaterAI si no existe).
// Cada ~0.15 s decide qué ROL hace cada jugador y a qué POSICIÓN debe ir, de forma coordinada:
//
//  ATAQUE (la bola es nuestra)
//    Carrier  : el que lleva la bola
//    Finisher : busca el mejor sitio de remate delante de la portería
//    Winger   : se desmarca a un espacio libre y ancho, a distancia de pase
//    Safety   : espacio libre por detrás de la bola (pase atrás y cobertura)
//    -> cada rol evalúa una rejilla de puntos de la pista: espacio libre, línea de pase limpia,
//       distancia de pase, separación con compañeros (reparte al equipo) y, según el rol, tiro/anchura/progresión.
//
//  DEFENSA (la bola es del rival)
//    Hunter   : UN solo jugador presiona la bola (y solo si está a menos de 'pressRadius')
//    Cover    : sobre la línea bola-portería, escalonado por detrás del presionador
//    Sweeper  : último hombre, delante de nuestra portería, sin salir de ahí
//    Marker   : marca (por el lado de la portería) a los rivales más peligrosos y sin cubrir
//
//  BOLA SUELTA: Hunter (el que llega antes) + postura de ataque o defensa según quién la tocó último.
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

    [Header("Ataque: desmarques")]
    public float spotStep = 3f;            // paso de la rejilla de búsqueda de espacios (m)
    public float teammateSpacing = 6f;     // distancia a la que los compañeros se estorban (m)

    [Header("Defensa")]
    public float pressRadius = 16f;        // al rival con bola solo se le presiona si algún defensa está a menos de esto (m)
    public float sweeperDepth = 8f;        // distancia del último hombre a nuestra portería (m)

    [Header("Geometría")]
    public float goalHalfWidth = 0.85f;    // mitad del ancho de la portería
    public float goalAvoidExtra = 1.2f;    // margen extra (m) que la IA deja alrededor de la zona prohibida de las porterías
    public float rinkMargin = 1.2f;
    public Vector2 fallbackRinkHalfSize = new Vector2(19f, 9f); // solo si GameManager.rinkWalls no está asignado

    [Header("Debug")]
    public bool drawGizmos = true;

    TeamManager tm;
    readonly Dictionary<Skater, AIAssignment> assignments = new Dictionary<Skater, AIAssignment>();
    readonly Dictionary<Skater, AIRole> lastRoles = new Dictionary<Skater, AIRole>();
    readonly Dictionary<AIRole, Vector2> lastSpot = new Dictionary<AIRole, Vector2>();
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

    float LateralHalfExtent()
    {
        Vector2 lat = Lateral;
        return Mathf.Abs(lat.x) * (rinkMax.x - rinkMin.x) * 0.5f +
               Mathf.Abs(lat.y) * (rinkMax.y - rinkMin.y) * 0.5f;
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

    // ¿Está 'p' dentro de la red de alguna portería (o a menos de 'extra' de ella)?
    public bool IsInGoalZone(Vector2 p, float extra)
    {
        Vector2 q = p;
        Vector2 n;
        for (int i = 0; i < Goal.All.Count; i++)
            if (Goal.All[i].TryPushOut(ref q, extra, out n)) return true;
        return false;
    }

    // Posición válida para la IA: dentro de la pista y fuera de la zona de las porterías
    public Vector2 Sanitize(Vector2 p)
    {
        p = ClampToRink(p);

        float extra = M(goalAvoidExtra);
        Vector2 n;
        for (int i = 0; i < Goal.All.Count; i++)
            Goal.All[i].TryPushOut(ref p, extra, out n);

        return ClampToRink(p);
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
            // 2) Cazador de la bola (solo uno)
            Skater hunter = PickHunter(ball);
            if (hunter != null)
            {
                assignments[hunter] = new AIAssignment(AIRole.Hunter, Sanitize(InterceptPoint(hunter, ball)));
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

        // Rival con la bola y ningún defensa cerca: nadie sale a presionar, el equipo se coloca
        if (best != null && ball.IsHeld &&
            Vector2.Distance(best.transform.position, ball.rb.position) > M(pressRadius))
            return null;

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

    // =====================================================================
    //  ATAQUE: desmarques a espacios libres
    // =====================================================================

    void BuildAttackSlots(Vector2 carrierPos, List<AIAssignment> list)
    {
        // Orden = prioridad. Cada rol se coloca teniendo en cuenta a los anteriores (separación).
        list.Add(new AIAssignment(AIRole.Finisher, FindSpot(AIRole.Finisher, carrierPos)));
        list.Add(new AIAssignment(AIRole.Winger, FindSpot(AIRole.Winger, carrierPos)));
        list.Add(new AIAssignment(AIRole.Safety, FindSpot(AIRole.Safety, carrierPos)));
    }

    // Recorre una rejilla de puntos de la zona del rol y devuelve el mejor
    Vector2 FindSpot(AIRole role, Vector2 carrier)
    {
        Vector2 goal = OppGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;

        float cd = Mathf.Max(Vector2.Dot(goal - carrier, atk), 0f);           // distancia del portador a portería
        float carrierLat = Vector2.Dot(carrier - Center, lat);
        float latHalf = Mathf.Max(M(3f), LateralHalfExtent() - M(rinkMargin + 0.8f));

        // Zona de búsqueda de cada rol: ud = distancia a la línea de gol, v = posición lateral
        float udMin, udMax, vMin, vMax, minAbsV = 0f;
        switch (role)
        {
            case AIRole.Finisher:
                udMin = M(4f); udMax = M(9f);
                vMin = -M(6f); vMax = M(6f);
                break;
            case AIRole.Winger:
                udMin = M(6f); udMax = Mathf.Max(M(10f), Mathf.Min(cd + M(3f), M(22f)));
                vMin = -latHalf; vMax = latHalf;
                minAbsV = M(4f);                       // siempre abierto, nunca por el centro
                break;
            default: // Safety
                udMin = Mathf.Clamp(cd + M(3f), M(12f), M(24f));
                udMax = Mathf.Min(udMin + M(8f), M(30f));
                vMin = -M(7f); vMax = M(7f);
                break;
        }
        udMax = Mathf.Max(udMax, udMin);

        float step = M(spotStep);
        int nu = Mathf.Max(1, Mathf.CeilToInt((udMax - udMin) / step)) + 1;
        int nv = Mathf.Max(1, Mathf.CeilToInt((vMax - vMin) / step)) + 1;

        Vector2 prev;
        bool hasPrev = lastSpot.TryGetValue(role, out prev);

        Vector2 best = Sanitize(goal - atk * udMin);
        float bestScore = float.MinValue;

        for (int i = 0; i < nu; i++)
        {
            float ud = Mathf.Min(udMin + i * step, udMax);
            for (int j = 0; j < nv; j++)
            {
                float v = Mathf.Min(vMin + j * step, vMax);
                if (Mathf.Abs(v) < minAbsV) continue;

                Vector2 p = Sanitize(goal - atk * ud + lat * v);
                float s = ScoreSpot(role, p, carrier, carrierLat, latHalf, hasPrev, prev);
                if (s > bestScore) { bestScore = s; best = p; }
            }
        }

        lastSpot[role] = best;
        taken.Add(best);
        return best;
    }

    float ScoreSpot(AIRole role, Vector2 p, Vector2 carrier, float carrierLat, float latHalf,
                    bool hasPrev, Vector2 prev)
    {
        float open = Mathf.Clamp01(tm.DistanceToNearestOpponent(p) / M(6f));   // espacio libre
        float lane = LaneClear(carrier, p);                                    // línea de pase limpia

        // Distancia de pase razonable: ni pegado al portador ni demasiado lejos
        float dc = Vector2.Distance(p, carrier);
        float range = 1f;
        if (dc < M(4f)) range = dc / M(4f);
        else if (dc > M(16f)) range = Mathf.Clamp01(1f - (dc - M(16f)) / M(8f));

        float s = 1.6f * open + 1.4f * lane + 1f * range;

        // Separación respecto a compañeros ya colocados: reparte al equipo por la pista
        float sp = M(teammateSpacing);
        for (int i = 0; i < taken.Count; i++)
        {
            float d = Vector2.Distance(p, taken[i]);
            if (d < sp) s -= 1.5f * (1f - d / sp);
        }

        float v = Vector2.Dot(p - Center, Lateral);
        float ud = Vector2.Dot(OppGoalPos - p, AttackDir);

        if (role == AIRole.Finisher)
        {
            Vector2 aim;
            s += 2f * ShotQuality(p, out aim);
        }
        else if (role == AIRole.Winger)
        {
            s += 0.8f * Mathf.Clamp01(Mathf.Abs(v) / latHalf);                      // anchura
            s += 0.5f * Mathf.Clamp01(Mathf.Abs(v - carrierLat) / M(8f));           // lado distinto al del portador
            s += 0.6f * (1f - Mathf.Clamp01(ud / M(22f)));                          // progresar
        }

        // Estabilidad: no cambiar de sitio sin motivo
        if (hasPrev) s += 0.6f * (1f - Mathf.Clamp01(Vector2.Distance(p, prev) / M(6f)));

        return s;
    }

    // =====================================================================
    //  DEFENSA: capas en vez de todos a la bola
    // =====================================================================

    void BuildDefenseSlots(Vector2 ballPos, Ball ball, List<AIAssignment> list)
    {
        Vector2 own = OwnGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;
        Vector2 carrier = ball.IsHeld ? (Vector2)ball.Holder.transform.position : ballPos;

        // Cobertura: sobre la línea bola-portería, escalonada por detrás del presionador
        Vector2 toGoal = own - carrier;
        float dist = toGoal.magnitude;
        Vector2 dirG = dist < 0.1f ? -atk : toGoal / dist;
        float coverDist = Mathf.Clamp(dist * 0.45f, M(4f), M(8f));
        Vector2 cover = Sanitize(carrier + dirG * coverDist);
        list.Add(new AIAssignment(AIRole.Cover, cover));
        taken.Add(cover);

        // Último hombre: delante de nuestra portería, desplazado hacia el lado de la bola
        float ballLat = Vector2.Dot(carrier - Center, lat);
        Vector2 sweeper = Sanitize(own + atk * M(sweeperDepth) + lat * Mathf.Clamp(ballLat * 0.4f, -M(4f), M(4f)));
        list.Add(new AIAssignment(AIRole.Sweeper, sweeper));
        taken.Add(sweeper);

        if (tm.opponent == null) return;

        // Marcajes: rivales más peligrosos (cerca de nuestra portería, con línea de pase libre)
        // y que no estén ya cubiertos por la cobertura o el último hombre
        Skater ballOpp = ball.IsHeld ? ball.Holder : NearestOpponentTo(ballPos);
        Skater m1 = null, m2 = null;
        float s1 = float.MinValue, s2 = float.MinValue;

        foreach (Skater o in tm.opponent.skaters)
        {
            if (o == ballOpp) continue;

            Vector2 op = o.transform.position;
            float danger = 0.6f * (1f - Mathf.Clamp01(Vector2.Distance(op, own) / M(30f))) + LaneClear(carrier, op);
            if (Vector2.Distance(op, cover) < M(2.5f) || Vector2.Distance(op, sweeper) < M(2.5f)) danger -= 0.8f;

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
        return new AIAssignment(AIRole.Marker, Sanitize(op + dir * M(1.5f)));
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
            case AIRole.Sweeper: return Color.green;
            default: return new Color(1f, 0.5f, 0f);   // Marker
        }
    }
}