using System.Collections.Generic;
using UnityEngine;

public enum AIRole { Carrier, Hunter, Finisher, Winger, Safety, Cover, Sweeper, Marker }

public struct AIAssignment
{
    public AIRole role;
    public Vector2 target;
    public AIAssignment(AIRole role, Vector2 target) { this.role = role; this.target = target; }
}

// Cerebro del equipo. Va en el GameObject del TeamManager (lo crea SkaterAI si no existe).
// Cada ~0.15 s decide qué ROL hace cada jugador y a qué POSICIÓN debe ir.
//
//  ATAQUE (la bola es nuestra)
//    Carrier  : el que lleva la bola
//    Finisher : remate; prefiere el lado que el portero NO cubre (segundo palo) -> premia el pase cruzado
//    Winger   : desmarque abierto, a distancia de pase
//    Safety   : apoyo por detrás de la bola
//    Las posiciones se calculan respecto a donde ESTARÁ el portador (posición + velocidad x supportLead),
//    así los compañeros se adelantan a la jugada en vez de ir detrás de la bola.
//
//  DEFENSA (la bola es del rival) -> bloque, no acoso
//    Hunter   : UN jugador sigue al portador. Lo CONTIENE a distancia por el lado de la portería y
//               solo entra al robo dentro de la zona de presión (pressLine) o si la bola queda expuesta.
//    Cover    : sobre la línea bola-portería, escalonado
//    Sweeper  : último hombre
//    Marker   : marca por el lado de la portería a 'markDistance' (deja respirar al rival)
//
//  POSESIÓN: lleva un reloj de posesión (PossessionTime). SkaterAI lo usa como "urgencia" para finalizar.
[RequireComponent(typeof(TeamManager))]
public class TeamBrain : MonoBehaviour
{
    [Header("Perfil (ScriptableObject)")]
    [SerializeField] AIProfile profile;

    [Header("Escala")]
    public float referenceRinkLength = 40f;
    public bool scaleWithRink = false;

    [Header("Reparto de roles")]
    public float updateInterval = 0.15f;
    public float roleStickiness = 2f;
    public float hunterStickiness = 0.8f;
    [Tooltip("Si el jugador humano está a menos de esto del portador rival, él hace de Hunter y la IA no dobla")]
    public float humanPressRadius = 4f;

    [Header("Ataque: desmarques")]
    public float spotStep = 3f;
    public float teammateSpacing = 7f;
    [Tooltip("Segundos hacia el futuro con los que se anticipa la posición del portador")]
    public float supportLead = 0.6f;

    [Header("Defensa")]
    public float sweeperDepth = 7f;

    [Header("Defensa en zona (si Defense Shape no es ManMarking)")]
    public DefenseShape defenseShape = DefenseShape.ManMarking;
    [Range(0f, 1f), Tooltip("Cuánto sale el bloque hacia la bola (0 = pegado a la portería)")]
    public float zoneFollow = 0.4f;
    public float zoneDepthMin = 4.5f;    // centro del bloque: distancia mínima a nuestra portería (m)
    public float zoneDepthMax = 11f;     // ... y máxima
    public float zoneHalfWidth = 2.8f;   // semiancho del bloque (m)
    public float zoneHalfLength = 2.2f;  // semilargo del bloque (m)
    [Tooltip("Si el humano está así de cerca de un vértice, lo ocupa él")]
    public float humanFillRadius = 2.5f;

    readonly List<Vector2> zonePts = new List<Vector2>(4);
    readonly List<Skater> markCandidates = new List<Skater>(8);

    [Header("Geometría")]
    public float goalHalfWidth = 0.85f;
    public float goalAvoidExtra = 1.2f;   // margen para posiciones "de reposo"
    public float tightGoalExtra = 0.1f;   // margen para ir a por bolas pegadas a la red
    public float rinkMargin = 1.2f;
    public Vector2 fallbackRinkHalfSize = new Vector2(19f, 9f);

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

    bool hasPossession;
    float possessionStart;

    public float Unit { get; private set; } = 1f;
    public float M(float meters) => meters * Unit;

    // Perezoso: si nadie asignó un perfil (o SkaterAI lo pide antes que nuestro Awake) se crea uno por defecto
    public AIProfile Profile
    {
        get
        {
            if (profile == null) profile = ScriptableObject.CreateInstance<AIProfile>();
            return profile;
        }
    }

    public float PossessionTime => hasPossession ? Time.time - possessionStart : 0f;
    public int BackPassesInRow { get; private set; }
    public void RegisterPass(bool backward) { BackPassesInRow = backward ? BackPassesInRow + 1 : 0; }

    // ---------- Geometría del campo ----------

    // Toda la IA trabaja con la BOCA de la portería (sobre la línea de gol), no con el Transform asignado
    // en el TeamManager, que puede estar dentro de la red. Apuntar a un punto detrás de la línea hace que
    // los tiros en ángulo crucen la línea FUERA del primer palo: el "tiro en diagonal".
    public Vector2 OwnGoalPos => MouthOf(tm.ownGoal);
    public Vector2 OppGoalPos => MouthOf(tm.opponentGoal);

    static Vector2 MouthOf(Transform t)
    {
        Goal g = Goal.Nearest(t.position);
        return g != null ? g.Mouth : (Vector2)t.position;
    }
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
        if (gm == null || gm.rinkWalls == null) return;

        Bounds b = gm.rinkWalls.bounds;
        rinkMin = b.min;
        rinkMax = b.max;
        Unit = scaleWithRink ? Mathf.Max(0.1f, b.size.x / referenceRinkLength) : 1f;
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

    public Vector2 ClampToRink(Vector2 p)
    {
        float m = M(rinkMargin);
        return new Vector2(Mathf.Clamp(p.x, rinkMin.x + m, rinkMax.x - m),
                           Mathf.Clamp(p.y, rinkMin.y + m, rinkMax.y - m));
    }

    public bool IsInGoalZone(Vector2 p, float extra)
    {
        Vector2 q = p;
        Vector2 n;
        for (int i = 0; i < Goal.All.Count; i++)
            if (Goal.All[i].TryPushOut(ref q, extra, out n)) return true;
        return false;
    }

    public Vector2 Sanitize(Vector2 p) => Sanitize(p, M(goalAvoidExtra));

    public Vector2 Sanitize(Vector2 p, float goalExtra)
    {
        p = ClampToRink(p);
        Vector2 n;
        for (int i = 0; i < Goal.All.Count; i++)
            Goal.All[i].TryPushOut(ref p, goalExtra, out n);
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

    // ¿Estamos lo bastante cerca de nuestra portería como para entrar al robo?
    public bool InPressZone(Vector2 p)
    {
        float span = Vector2.Distance(OwnGoalPos, OppGoalPos);
        float fromOwn = Vector2.Dot(p - OwnGoalPos, AttackDir);
        return fromOwn <= span * Profile.pressLine;
    }

    // ---------- Evaluación (la usa también SkaterAI) ----------

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

    // "Amenaza" de una posición (0..1): lo peligroso que es tener la bola ahí, sin mirar defensas ni portero.
    // Sirve para saber si un pase hace AVANZAR la jugada o solo mueve la bola de sitio.
    public float Threat(Vector2 p)
    {
        Vector2 goal = OppGoalPos;
        float along = Vector2.Dot(goal - p, AttackDir);
        if (along < M(0.5f)) return 0.15f;                         // detrás de la portería

        float d = Vector2.Distance(p, goal);
        float distF = 1f - Mathf.Clamp01((d - M(3f)) / M(17f));   // 1 a 3 m, 0 a 20 m
        float angleF = Mathf.Clamp01((along / d - 0.2f) / 0.6f);
        return distF * (0.4f + 0.6f * angleF);
    }

    float ShotSpeedRef => tm.skaters.Count > 0 ? tm.skaters[0].shootForce : 25f;

    public float ShotQuality(Vector2 from, out Vector2 aimPoint) => ShotQuality(from, ShotSpeedRef, out aimPoint);

    // Calidad de tiro (0..1) y punto al que apuntar. Para cada palo estima si el portero LLEGA:
    //   tiempo de vuelo = distancia / velocidad del tiro
    //   tiempo del portero = reacción + (hueco que tiene que cubrir) / velocidad de estirada
    // Si el portero llega tarde, buen tiro. Por eso un pase cruzado (portero a contrapié) da tiros buenos.
    public float ShotQuality(Vector2 from, float shotSpeed, out Vector2 aimPoint)
    {
        Vector2 goal = OppGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;
        aimPoint = goal;

        float along = Vector2.Dot(goal - from, atk);
        if (along < M(0.8f)) return 0f;

        float d = Vector2.Distance(from, goal);
        float distF = 1f - Mathf.Clamp01((d - M(3f)) / M(10f));   // 1 a <=3 m, 0 a >=13 m
        float angleF = Mathf.Clamp01((along / d - 0.3f) / 0.6f);

        float keeperLat = 0f, gkReact = 0.17f, gkSpeed = 5.5f, gkCover = 0.5f;
        Goalkeeper gk = tm.opponent != null ? tm.opponent.goalkeeper : null;
        if (gk != null)
        {
            keeperLat = Vector2.Dot((Vector2)gk.transform.position - goal, lat);
            gkReact = gk.ReactionTime;
            gkSpeed = Mathf.Max(gk.speed, 0.1f);
            gkCover = gk.HalfWidth + 0.15f;   // medio cuerpo + radio aproximado de la bola
        }

        float aimLat = Mathf.Max(0.05f, goalHalfWidth - Profile.postMargin);
        float best = 0f;

        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 p = goal + lat * (side * aimLat);

            float laneF = 0.35f + 0.65f * LaneClear(from, p);
            float flight = Vector2.Distance(from, p) / Mathf.Max(shotSpeed, 1f);
            float gap = Mathf.Max(0f, Mathf.Abs(side * aimLat - keeperLat) - gkCover);
            float keeperTime = gkReact + gap / gkSpeed;
            float beatF = Mathf.Clamp(0.35f + (keeperTime - flight) * 4f, 0.1f, 1f);

            float q = distF * angleF * laneF * beatF;
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
        TrackPossession(ball);

        bool possessionChanged = ball.Holder != lastHolder;
        if (!possessionChanged && Time.time < nextUpdate) return;

        lastHolder = ball.Holder;
        nextUpdate = Time.time + updateInterval;
        Recompute(ball);
    }

    void TrackPossession(Ball ball)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.State != MatchState.Playing)
        {
            hasPossession = false;   // el reloj de posesión empieza cuando se juega
            return;
        }

        bool ours = ball.IsHeld ? ball.Holder.team == tm.team : ball.LastTouchTeam == tm.team;
        if (ours && !hasPossession)
        {
            hasPossession = true;
            possessionStart = Time.time;
            BackPassesInRow = 0;
        }
        else if (!ours)
        {
            hasPossession = false;
        }
    }

    void Recompute(Ball ball)
    {
        assignments.Clear();
        taken.Clear();
        free.Clear();

        foreach (Skater s in tm.skaters)
        {
            if (s.IsControlled) taken.Add(s.transform.position);
            else free.Add(s);
        }

        Vector2 ballPos = ball.rb.position;
        bool ours = ball.IsHeld && ball.Holder.team == tm.team;
        bool attackingPosture = ours || (!ball.IsHeld && ball.LastTouchTeam == tm.team);

        if (ours)
        {
            taken.Add(ballPos);
            if (free.Remove(ball.Holder))
                assignments[ball.Holder] = new AIAssignment(AIRole.Carrier, ballPos);
            currentHunter = null;
        }
        else
        {
            Skater hunter = HumanIsPressing(ball) ? null : PickHunter(ball);
            if (hunter != null)
            {
                Vector2 t = ball.IsHeld ? ContainPoint(ball.Holder) : Sanitize(InterceptPoint(hunter, ball), M(tightGoalExtra));
                assignments[hunter] = new AIAssignment(AIRole.Hunter, t);
                free.Remove(hunter);
            }
            currentHunter = hunter;
        }

        slots.Clear();
        Vector2 refPos = ballPos;
        if (ball.IsHeld)
        {
            Skater h = ball.Holder;
            refPos = ClampToRink((Vector2)h.transform.position + (ours ? h.Velocity * supportLead : Vector2.zero));
        }

        if (attackingPosture) BuildAttackSlots(refPos, slots);
        else if (defenseShape != DefenseShape.ManMarking) BuildZoneSlots(ballPos, ball, currentHunter != null, slots);
        else BuildDefenseSlots(ballPos, ball, slots);



        while (slots.Count > free.Count) slots.RemoveAt(slots.Count - 1);
        while (slots.Count < free.Count)
            slots.Add(new AIAssignment(AIRole.Safety, Sanitize(Vector2.Lerp(OwnGoalPos, Center, 0.4f))));

        Assign(free, slots);

        foreach (KeyValuePair<Skater, AIAssignment> kv in assignments)
            lastRoles[kv.Key] = kv.Value.role;
    }

    // ---------- Cazador / contención ----------

    // Punto de contención: entre el portador y nuestra portería, a containDistance
    public Vector2 ContainPoint(Skater carrier)
    {
        Vector2 cp = carrier.transform.position;
        Vector2 toGoal = OwnGoalPos - cp;
        Vector2 dir = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : -AttackDir;
        return Sanitize(cp + dir * M(Profile.containDistance), M(tightGoalExtra));
    }

    Vector2 InterceptPoint(Skater s, Ball ball)
    {
        Vector2 p = ball.rb.position;
        if (ball.IsHeld) return ClampToRink(p + ball.Holder.Velocity * 0.3f);

        Vector2 v = ball.rb.linearVelocity;
        float damp = Mathf.Max(ball.rb.linearDamping, 0.01f);
        Vector2 target = p;
        for (int i = 0; i < 3; i++)
        {
            float t = Mathf.Min(Vector2.Distance(s.transform.position, target) / s.TopSpeed, 1.5f);
            float travel = (1f - Mathf.Exp(-damp * t)) / damp;
            target = p + v * travel;
        }
        return ClampToRink(target);
    }

    bool HumanIsPressing(Ball ball)
    {
        if (!ball.IsHeld || ball.Holder.team == tm.team) return false;
        Vector2 bp = ball.rb.position;
        foreach (Skater s in tm.skaters)
            if (s.IsControlled && Vector2.Distance(s.transform.position, bp) < M(humanPressRadius)) return true;
        return false;
    }

    Skater PickHunter(Ball ball)
    {
        Skater best = null;
        float bestT = float.MaxValue;
        foreach (Skater s in free)
        {
            float t = Vector2.Distance(s.transform.position, InterceptPoint(s, ball)) / s.TopSpeed;
            if (s == currentHunter) t *= hunterStickiness;
            if (t < bestT) { bestT = t; best = s; }
        }

        if (best != null && ball.IsHeld &&
            Vector2.Distance(best.transform.position, ball.rb.position) > M(Profile.pressRadius))
            return null;

        if (best != null && !ball.IsHeld)
        {
            foreach (Skater s in tm.skaters)
            {
                if (!s.IsControlled) continue;
                float th = Vector2.Distance(s.transform.position, InterceptPoint(s, ball)) / s.TopSpeed;
                if (th < bestT * 0.8f) return null;
            }
        }
        return best;
    }

    // =====================================================================
    //  ATAQUE
    // =====================================================================

    void BuildAttackSlots(Vector2 carrierPos, List<AIAssignment> list)
    {
        list.Add(new AIAssignment(AIRole.Finisher, FindSpot(AIRole.Finisher, carrierPos)));
        list.Add(new AIAssignment(AIRole.Winger, FindSpot(AIRole.Winger, carrierPos)));
        list.Add(new AIAssignment(AIRole.Safety, FindSpot(AIRole.Safety, carrierPos)));
    }

    Vector2 FindSpot(AIRole role, Vector2 carrier)
    {
        Vector2 goal = OppGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;

        float cd = Mathf.Max(Vector2.Dot(goal - carrier, atk), 0f);
        float carrierLat = Vector2.Dot(carrier - Center, lat);
        float latHalf = Mathf.Max(M(3f), LateralHalfExtent() - M(rinkMargin + 0.8f));

        float udMin, udMax, vMin, vMax, minAbsV = 0f;
        switch (role)
        {
            case AIRole.Finisher:
                udMin = M(3.5f); udMax = M(9f);
                vMin = -M(6f); vMax = M(6f);
                break;
            case AIRole.Winger:
                udMin = M(5f); udMax = Mathf.Max(M(10f), Mathf.Min(cd + M(4f), M(22f)));
                vMin = -latHalf; vMax = latHalf;
                minAbsV = M(4f);
                break;
            default: // Safety
                udMin = Mathf.Clamp(cd + M(4f), M(12f), M(24f));
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
                float s = ScoreSpot(role, p, carrier, carrierLat, cd, latHalf, hasPrev, prev);
                if (s > bestScore) { bestScore = s; best = p; }
            }
        }

        lastSpot[role] = best;
        taken.Add(best);
        return best;
    }

    float ScoreSpot(AIRole role, Vector2 p, Vector2 carrier, float carrierLat, float carrierUd,
                    float latHalf, bool hasPrev, Vector2 prev)
    {
        float nearestOpp = tm.DistanceToNearestOpponent(p);
        float open = Mathf.Clamp01(nearestOpp / M(6f));
        float lane = LaneClear(carrier, p);

        float dc = Vector2.Distance(p, carrier);
        float range;
        if (dc < M(5f)) range = dc / M(5f) - 1f;
        else if (dc <= M(13f)) range = 1f;
        else range = Mathf.Clamp01(1f - (dc - M(13f)) / M(8f));

        float s = 1.2f * open + 3f * lane + 1.2f * range;
        if (lane < 0.6f) s -= 2.5f * (0.6f - lane) / 0.6f;

        float markR = M(2.5f);
        if (nearestOpp < markR) s -= 1.5f * (1f - nearestOpp / markR);

        float sp = M(teammateSpacing);
        Vector2 dirP = p - carrier;
        for (int i = 0; i < taken.Count; i++)
        {
            float d = Vector2.Distance(p, taken[i]);
            if (d < sp) s -= 1.5f * (1f - d / sp);

            Vector2 dirT = taken[i] - carrier;
            if (dirP.magnitude > M(3f) && dirT.magnitude > M(3f))
            {
                float ang = Vector2.Angle(dirP, dirT);
                if (ang < 30f) s -= 1f * (1f - ang / 30f);
            }
        }

        float v = Vector2.Dot(p - Center, Lateral);
        float ud = Vector2.Dot(OppGoalPos - p, AttackDir);

        if (role == AIRole.Finisher)
        {
            Vector2 aim;
            s += 2.5f * ShotQuality(p, out aim);   // ya incluye "el portero no llega a ese lado"

            // Segundo palo: si el portador está cerca de la portería, ofrecerse en el lado contrario
            if (carrierUd < M(12f))
                s += 0.8f * Mathf.Clamp01(Mathf.Abs(v - carrierLat) / M(5f));
        }
        else if (role == AIRole.Winger)
        {
            s += 0.8f * Mathf.Clamp01(Mathf.Abs(v) / latHalf);
            s += 0.5f * Mathf.Clamp01(Mathf.Abs(v - carrierLat) / M(8f));
            s += 0.6f * (1f - Mathf.Clamp01(ud / M(22f)));
        }

        if (hasPrev) s += 0.4f * (1f - Mathf.Clamp01(Vector2.Distance(p, prev) / M(6f)));
        return s;
    }

    // =====================================================================
    //  DEFENSA: bloque compacto, no acoso
    // =====================================================================

    void BuildDefenseSlots(Vector2 ballPos, Ball ball, List<AIAssignment> list)
    {
        AIProfile pr = Profile;
        Vector2 own = OwnGoalPos;
        Vector2 atk = AttackDir;
        Vector2 lat = Lateral;
        Vector2 carrier = ball.IsHeld ? (Vector2)ball.Holder.transform.position : ballPos;

        Vector2 toGoal = own - carrier;
        float dist = toGoal.magnitude;
        Vector2 dirG = dist < 0.1f ? -atk : toGoal / dist;
        float coverDist = Mathf.Clamp(dist * 0.45f, M(pr.coverMin), M(pr.coverMax));
        Vector2 cover = Sanitize(carrier + dirG * coverDist);
        list.Add(new AIAssignment(AIRole.Cover, cover));
        taken.Add(cover);

        float ballLat = Vector2.Dot(carrier - Center, lat);
        Vector2 sweeper = Sanitize(own + atk * M(sweeperDepth) + lat * Mathf.Clamp(ballLat * 0.4f, -M(4f), M(4f)));
        list.Add(new AIAssignment(AIRole.Sweeper, sweeper));
        taken.Add(sweeper);

        if (tm.opponent == null) return;

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

        // Bloque en zona entre la bola y nuestra portería. El humano y el Hunter ocupan vértices.
    void BuildZoneSlots(Vector2 ballPos, Ball ball, bool aiHunter, List<AIAssignment> list)
    {
        Vector2 own = OwnGoalPos;
        Vector2 carrier = ball.IsHeld ? (Vector2)ball.Holder.transform.position : ballPos;
        Vector2 toBall = carrier - own;
        float dist = toBall.magnitude;
        Vector2 u = dist > 0.1f ? toBall / dist : AttackDir;   // eje portería -> bola
        Vector2 v = new Vector2(-u.y, u.x);                    // lateral

        // Centro del bloque: sale hacia la bola, pero la línea delantera nunca queda por delante de ella
        float depth = Mathf.Clamp(dist * zoneFollow, M(zoneDepthMin), M(zoneDepthMax));
        depth = Mathf.Min(depth, Mathf.Max(M(zoneDepthMin), dist - M(zoneHalfLength + 1.5f)));
        Vector2 c = own + u * depth;
        float w = M(zoneHalfWidth), l = M(zoneHalfLength);

        zonePts.Clear();
        if (defenseShape == DefenseShape.Box)
        {
            zonePts.Add(c + u * l + v * w); zonePts.Add(c + u * l - v * w);
            zonePts.Add(c - u * l + v * w); zonePts.Add(c - u * l - v * w);
        }
        else // Diamond
        {
            zonePts.Add(c + u * l * 1.3f); zonePts.Add(c + v * w);
            zonePts.Add(c - v * w);        zonePts.Add(c - u * l * 1.3f);
        }

        if (aiHunter) RemoveNearest(zonePts, carrier, float.MaxValue);   // el vértice de delante lo cubre el Hunter
        foreach (Skater s in tm.skaters)
            if (s.IsControlled) RemoveNearest(zonePts, s.transform.position, M(humanFillRadius));   // el humano rellena el suyo

        foreach (Vector2 p in zonePts)
        {
            Vector2 q = Sanitize(p);
            bool back = Vector2.Dot(p - c, u) < 0f;
            list.Add(new AIAssignment(back ? AIRole.Sweeper : AIRole.Cover, q));
            taken.Add(q);
        }

        AddMarkers(list, ball, carrier, own, 2);   // los que sobren marcan a los rivales peligrosos fuera del bloque
    }

    static void RemoveNearest(List<Vector2> pts, Vector2 p, float maxDist)
    {
        int best = -1;
        float bestD = maxDist;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = Vector2.Distance(pts[i], p);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best >= 0) pts.RemoveAt(best);
    }

    void AddMarkers(List<AIAssignment> list, Ball ball, Vector2 carrier, Vector2 own, int count)
    {
        if (tm.opponent == null) return;
        Skater ballOpp = ball.IsHeld ? ball.Holder : NearestOpponentTo(ball.rb.position);

        markCandidates.Clear();
        foreach (Skater o in tm.opponent.skaters)
        {
            if (o == ballOpp) continue;
            Vector2 op = o.transform.position;
            bool covered = false;
            for (int i = 0; i < taken.Count; i++)
                if (Vector2.Distance(op, taken[i]) < M(2.5f)) { covered = true; break; }
            if (!covered) markCandidates.Add(o);
        }

        markCandidates.Sort((a, b) => Danger(b, own, carrier).CompareTo(Danger(a, own, carrier)));
        for (int i = 0; i < markCandidates.Count && i < count; i++)
            list.Add(MarkSlot(markCandidates[i], own, carrier));
    }

    float Danger(Skater o, Vector2 own, Vector2 carrier)
    {
        Vector2 op = o.transform.position;
        return 0.6f * (1f - Mathf.Clamp01(Vector2.Distance(op, own) / M(30f))) + LaneClear(carrier, op);
    }

    AIAssignment MarkSlot(Skater o, Vector2 own, Vector2 carrier)
    {
        Vector2 op = o.transform.position;
        Vector2 goalSide = (own - op).normalized;
        Vector2 ballSide = (carrier - op).normalized;
        Vector2 dir = (goalSide * 0.7f + ballSide * 0.3f).normalized;
        return new AIAssignment(AIRole.Marker, Sanitize(op + dir * M(Profile.markDistance)));
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
            default: return new Color(1f, 0.5f, 0f);
        }
    }
}

public enum DefenseShape { ManMarking, Box, Diamond }