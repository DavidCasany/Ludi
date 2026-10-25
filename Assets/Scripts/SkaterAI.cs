using UnityEngine;

// Ejecuta lo que decide el TeamBrain.
//  - Sin bola: va a la posición de su rol rodeando las porterías (GoalNav). Si es el Hunter contra un
//    portador rival, lo CONTIENE a distancia y solo entra al robo cuando toca (zona de presión + retraso,
//    o bola expuesta). Ya no "se tira" a por la bola a cada momento.
//  - Con bola: piensa al recibir, y compara TIRAR / PASAR / CONDUCIR con una función de "amenaza".
//    Un pase solo se da si hace avanzar la jugada (o si le presionan). Con el tiempo de posesión crece la
//    urgencia: baja el umbral de tiro y conduce hacia la zona de remate. Resultado: las posesiones terminan.
[RequireComponent(typeof(Skater))]
public class SkaterAI : MonoBehaviour
{
    [Header("Nivel de la IA")]
    [Range(0f, 1f)] public float skill = 0.7f;   // puntería, reacción y regates

    [Header("Con bola")]
    public float pressureDistance = 2f;          // rival a menos de esto = presionado (m)
    public float ownZoneDistance = 12f;          // "mi zona" para despejar (m)
    public float shootZone = 9f;                 // zona de remate (m)
    public float shootPatience = 1.2f;           // en zona de remate, tras este tiempo tira igualmente

    [Header("Regate y sprint")]
    [Range(0f, 1f)] public float jukeChance = 0.6f;
    public float sprintMinStamina = 0.3f;

    [Header("Stick (robo e intercepción)")]
    public float stickReaction = 0.25f;
    [Range(0f, 1f)] public float interceptChance = 0.5f;

    [Header("Saque de centro")]
    public float kickoffDelay = 0.35f;

    [Header("Debug")]
    public bool debugLog = false;

    const float BodyClearance = 0.45f;   // radio aproximado del jugador para rodear porterías

    Skater me;
    TeamManager tm;
    TeamBrain brain;
    bool hadBall;
    float ballSince;
    float nextDecision;
    float nextLog;
    Vector2 dribbleDir = Vector2.right;

    // Conducción comprometida
    Vector2 dribbleTarget;
    float dribbleUntil;
    int lastLane;

    // Stick y contención
    float inReachSince = -1f;
    bool willPoke;
    Skater containing;
    float engageAt;

    // Debug del último tiro (se ve en la vista Scene con Gizmos activados)
    Vector2 dbgFrom, dbgAim, dbgDir;
    float dbgUntil;

    AIProfile P => brain.Profile;

    void Awake()
    {
        me = GetComponent<Skater>();
        tm = GetComponentInParent<TeamManager>();

        if (tm != null)
        {
            brain = tm.GetComponent<TeamBrain>();
            if (brain == null) brain = tm.gameObject.AddComponent<TeamBrain>();
        }

        if (brain != null) me.stealChanceScale = brain.Profile.stealChanceScale;
    }

    void FixedUpdate()
    {
        Ball ball = Ball.Instance;
        if (ball == null || tm == null || brain == null) return;

        if (me.HasBall && !hadBall)
        {
            ballSince = Time.time;
            dribbleUntil = 0f;
        }
        hadBall = me.HasBall;

        GameManager gm = GameManager.Instance;
        if (me.HasBall && gm != null && gm.IsServing(me))
        {
            me.MoveInput = Vector2.zero;
            me.SprintInput = false;
            if (Time.time - ballSince > kickoffDelay) me.PassTo(PickServeTarget());
            return;
        }

        if (me.HasBall)
        {
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + 0.1f + Random.Range(0f, 0.04f);
                DecideWithBall(ball);
            }
            return;
        }

        OffBall(ball);
    }

    // =====================================================================
    //  SIN BOLA
    // =====================================================================

    void OffBall(Ball ball)
    {
        AIAssignment a;
        if (!brain.TryGet(me, out a)) a = new AIAssignment(AIRole.Safety, me.Anchor);

        bool hunter = a.role == AIRole.Hunter;
        bool vsCarrier = hunter && ball.IsHeld && ball.Holder.team != me.team;

        if (vsCarrier)
        {
            Contain(ball);
        }
        else
        {
            containing = null;
            float dist = Vector2.Distance(transform.position, a.target);
            bool runner = hunter || a.role == AIRole.Winger || a.role == AIRole.Finisher;
            me.SprintInput = runner && dist > brain.M(hunter ? 4f : 6f) &&
                             me.Stamina01 > sprintMinStamina && !me.IsExhausted;

            float slow = hunter ? 0.01f : brain.M(1.5f);
            GoTo(a.target, slow, !hunter, Vector2.zero);

            Vector2 toBall = ball.rb.position - (Vector2)transform.position;
            if (toBall.sqrMagnitude > 0.01f) me.AimDir = toBall.normalized;
        }

        TryStick(ball);
    }

    // Contención: se coloca entre el portador y la portería, acompaña su velocidad y solo entra al robo
    // si (está en zona de presión Y ha pasado el retraso) o el portador queda descolocado o expone la bola.
    void Contain(Ball ball)
    {
        Skater carrier = ball.Holder;
        if (carrier != containing)
        {
            containing = carrier;
            engageAt = Time.time + Random.Range(P.engageDelay.x, P.engageDelay.y) * Mathf.Lerp(1.3f, 0.8f, skill);
        }

        Vector2 pos = transform.position;
        Vector2 cp = carrier.transform.position;
        Vector2 ballPos = ball.rb.position;
        float exposure = carrier.BallExposureTo(pos);

        bool zone = brain.InPressZone(cp);
        bool engage = (zone && Time.time >= engageAt) || carrier.IsStaggered ||
                      (exposure >= P.pokeExposure && Vector2.Distance(pos, ballPos) < me.stickReach * 1.6f);

        Vector2 target;
        if (engage)
        {
            Vector2 goalSide = (brain.OwnGoalPos - cp).normalized;
            target = brain.Sanitize(ballPos + goalSide * me.stickReach * 0.5f, brain.M(brain.tightGoalExtra));
        }
        else
        {
            target = brain.ContainPoint(carrier);
        }

        float dist = Vector2.Distance(pos, target);
        me.SprintInput = dist > brain.M(5f) && me.Stamina01 > sprintMinStamina && !me.IsExhausted;

        // Feed-forward: acompaña la velocidad del portador para no quedarse atrás ni pasarse
        Vector2 ff = carrier.Velocity / Mathf.Max(me.TopSpeed, 0.1f) * 0.8f;
        GoTo(target, brain.M(1.2f), false, ff);

        Vector2 toBall = ballPos - pos;
        if (toBall.sqrMagnitude > 0.01f) me.AimDir = toBall.normalized;
    }

    // Stick con tiempo de reacción. Contra un portador solo golpea si la bola está expuesta:
    // golpes con sentido, no al azar, y el humano puede protegerse orientando la bola.
    void TryStick(Ball ball)
    {
        if (!me.CanStick)
        {
            inReachSince = -1f;
            return;
        }

        Vector2 toBall = ball.rb.position - (Vector2)transform.position;
        bool inReach = toBall.magnitude <= me.stickReach * 0.95f &&
                       Vector2.Angle(me.AimDir, toBall) <= me.stickArc * 0.5f;

        if (!inReach)
        {
            inReachSince = -1f;
            return;
        }

        if (inReachSince < 0f)
        {
            inReachSince = Time.time;

            if (ball.IsHeld)
            {
                willPoke = ball.Holder.team != me.team;
            }
            else
            {
                willPoke = ball.Speed > me.autoPickupMaxSpeed && !ball.IsPassFor(me.team) &&
                           Random.value < interceptChance;
            }
        }

        float reaction = stickReaction * Mathf.Lerp(1.5f, 0.6f, skill);
        if (!willPoke || Time.time - inReachSince < reaction) return;

        if (ball.IsHeld)
        {
            Skater c = ball.Holder;
            if (c.team == me.team) { inReachSince = -1f; return; }

            bool exposed = !c.IsProtected && c.BallExposureTo(transform.position) >= P.pokeExposure;
            if (!exposed) return;   // espera su momento sin perder el golpe
        }

        me.Stick();
        inReachSince = -1f;
    }

    // =====================================================================
    //  CON BOLA
    // =====================================================================

    void DecideWithBall(Ball ball)
    {
        Vector2 pos = transform.position;
        Vector2 ballPos = ball.rb.position;
        Vector2 goal = brain.OppGoalPos;
        float held = Time.time - ballSince;

        Vector2 aimPoint;
        float q = brain.ShotQuality(ballPos, me.shootForce, out aimPoint);
        float oppDist = tm.DistanceToNearestOpponent(pos);
        bool pressured = oppDist < brain.M(pressureDistance);
        float urgency = Mathf.Clamp01(brain.PossessionTime / Mathf.Max(P.attackPatience, 0.1f));

        bool inZone = Vector2.Distance(pos, goal) < brain.M(shootZone) &&
                      Vector2.Dot(goal - pos, brain.AttackDir) > brain.M(1f);

        // Pensar al recibir (menos si el tiro es claro o le presionan)
        float think = P.thinkTime * Mathf.Lerp(1.4f, 0.7f, skill);
        if (q > 0.45f || pressured) think *= 0.4f;
        if (held < think)
        {
            Dribble(pos, oppDist, urgency, pressured);
            return;
        }

        float passValue, mateShot;
        bool backward;
        Skater mate = BestPass(out passValue, out mateShot, out backward);

        float freedom = Mathf.Clamp01((oppDist - brain.M(1f)) / brain.M(3f));
        float keepValue = 0.55f * brain.Threat(pos) + 0.45f * q + 0.25f * freedom;
        bool overheld = held > P.maxHoldTime;

        if (debugLog && Time.time >= nextLog)
        {
            nextLog = Time.time + 0.5f;
            Debug.Log($"{name}: q={q:F2} mateShot={mateShot:F2} pass={passValue:F2} keep={keepValue:F2} urg={urgency:F2} held={held:F1}s press={pressured}");
        }

        // 1) Tiro: umbral que baja con la urgencia
        float thr = Mathf.Lerp(P.shootThreshold, P.urgentShootThreshold, urgency);
        if (pressured) thr *= 0.8f;
        if (overheld) thr = Mathf.Min(thr, 0.06f);

        if (q >= thr && q + 0.1f >= mateShot)
        {
            ShootAt(aimPoint, pressured);
            return;
        }

        // 2) En zona de remate un rato: tira igualmente (un tiro con rebote vale más que otro pase)
        if (inZone && held > shootPatience && q > 0.05f)
        {
            ShootAt(aimPoint, pressured);
            return;
        }

        // 3) Pase: solo si hace avanzar la jugada (o si le presionan / lleva demasiado)
        if (mate != null)
        {
            bool tooManyBack = backward && brain.BackPassesInRow >= P.maxBackPassesInRow && !pressured;
            float gain = P.passGain * (pressured ? 0.2f : 1f);

            if (!tooManyBack && (passValue > keepValue + gain || overheld))
            {
                brain.RegisterPass(backward);
                me.PassTo(mate);
                return;
            }
        }

        // 4) Despeje: presionado en mi zona y sin pase
        bool inOwnZone = Vector2.Distance(pos, brain.OwnGoalPos) < brain.M(ownZoneDistance);
        if (pressured && inOwnZone && mate == null)
        {
            Clear();
            return;
        }

        // 5) Regate: presionado de frente
        if (pressured && me.CanJuke)
        {
            Skater near = NearestOpponent(pos);
            if (near != null)
            {
                Vector2 toOpp = ((Vector2)near.transform.position - pos).normalized;
                if (Vector2.Dot(dribbleDir, toOpp) > 0.2f &&
                    Random.value < jukeChance * Mathf.Lerp(0.5f, 1f, skill))
                {
                    me.Juke(Vector2.zero);
                    return;
                }
            }
        }

        // 6) Conducir
        Dribble(pos, oppDist, urgency, pressured);
    }

    // Mejor pase. El valor es la AMENAZA del receptor (posición + tiro + desmarque), comparable con keepValue.
    Skater BestPass(out float bestValue, out float mateShot, out bool bestBackward)
    {
        Skater best = null;
        bestValue = 0f;
        mateShot = 0f;
        bestBackward = false;

        Vector2 pos = transform.position;
        Vector2 atk = brain.AttackDir;

        foreach (Skater m in tm.skaters)
        {
            if (m == me) continue;

            Vector2 mp = m.transform.position;
            float dist = Vector2.Distance(pos, mp);
            if (dist < brain.M(2.5f)) continue;

            Vector2 lead = mp + m.Velocity * 0.4f;
            float lane = brain.LaneClear(pos, lead);
            if (lane < 0.35f) continue;

            Vector2 aim;
            float q = brain.ShotQuality(lead, m.shootForce, out aim);
            float open = Mathf.Clamp01(tm.DistanceToNearestOpponent(lead) / brain.M(4f));
            float range = dist > brain.M(20f) ? 0.6f : 1f;
            bool backward = Vector2.Dot(lead - pos, atk) < -brain.M(1f);

            mateShot = Mathf.Max(mateShot, q * lane);

            float value = lane * range * (0.55f * brain.Threat(lead) + 0.45f * q + 0.25f * open);
            if (backward) value -= P.backPassPenalty;

            if (value > bestValue) { bestValue = value; best = m; bestBackward = backward; }
        }
        return best;
    }

    Skater PickServeTarget()
    {
        float v, ms;
        bool back;
        Skater mate = BestPass(out v, out ms, out back);
        if (mate != null) return mate;

        Skater nearest = null;
        float bestD = float.MaxValue;
        foreach (Skater s in tm.skaters)
        {
            if (s == me) continue;
            float d = Vector2.Distance(s.transform.position, transform.position);
            if (d < bestD) { bestD = d; nearest = s; }
        }
        return nearest;
    }

    // ARREGLO CLAVE: la dirección del tiro se calcula desde la BOLA, no desde el centro del jugador.
    // La bola va delante del stick (hasta ~0.8-1 m de lado al protegerla); antes ese desfase mandaba
    // el tiro paralelo a la línea buena y fuera de la portería.
    void ShootAt(Vector2 aimPoint, bool pressured)
    {
        Ball ball = Ball.Instance;
        Vector2 from = ball != null ? ball.rb.position : (Vector2)transform.position;
        Vector2 dir = (aimPoint - from).normalized;

        // Error angular (distribución triangular: la mayoría de tiros cerca del objetivo)
        float err = P.aimErrorDeg * Mathf.Lerp(1.6f, 0.5f, skill) * (pressured ? 1.3f : 1f);
        float noise = (Random.Range(-err, err) + Random.Range(-err, err)) * 0.5f;
        dir = Rotate(dir, noise);

        dbgFrom = from; dbgAim = aimPoint; dbgDir = dir; dbgUntil = Time.time + 2f;
        if (debugLog) Debug.Log($"{name} TIRA desde {from} a {aimPoint} (error {noise:F1}°)");

        // Skater.Shoot suma parte de la velocidad del jugador: se compensa para clavar la dirección
        Vector2 desired = dir * me.shootForce;
        Vector2 cmd = desired - me.Velocity * me.inheritVelocity;
        me.Shoot(cmd.normalized, cmd.magnitude);
        brain.RegisterPass(false);
    }

    void Clear()
    {
        Vector2 pos = transform.position;
        Vector2 lat = brain.Lateral;
        float side = Vector2.Dot(pos - brain.Center, lat) >= 0f ? 1f : -1f;

        Vector2 target = pos + brain.AttackDir * brain.M(14f) + lat * side * brain.M(4f);
        Vector2 desired = (target - pos).normalized * me.shootForce * 0.85f;
        Vector2 cmd = desired - me.Velocity * me.inheritVelocity;
        me.Shoot(cmd.normalized, cmd.magnitude);
    }

    // Conducción con compromiso: elige un destino y lo mantiene 'commitTime' (salvo presión),
    // así no cambia de idea cada 0.1 s. Rodea las porterías y protege la bola con el cuerpo.
    void Dribble(Vector2 pos, float oppDist, float urgency, bool pressured)
    {
        Vector2 goal = brain.OppGoalPos;

        if (Time.time >= dribbleUntil || pressured)
        {
            dribbleTarget = DribbleTarget(pos, goal, urgency);
            dribbleUntil = Time.time + P.commitTime;
        }

        Vector2 waypoint = GoalNav.Route(pos, dribbleTarget, BodyClearance);
        Vector2 toT = waypoint - pos;

        if (toT.sqrMagnitude < 0.04f)
        {
            me.MoveInput = Vector2.zero;
            me.SprintInput = false;
            return;
        }

        Vector2 dir0 = toT.normalized;
        Vector2 best = dir0;
        float bestScore = float.MinValue;

        for (int i = -3; i <= 3; i++)
        {
            Vector2 dir = Rotate(dir0, i * 20f);
            Vector2 probe = pos + dir * brain.M(3f);

            float s = 1.2f * Vector2.Dot(dir, dir0);
            s += 1.5f * Mathf.Clamp01(tm.DistanceToNearestOpponent(probe) / brain.M(3f));
            if (!brain.IsInsideRink(probe, brain.M(0.5f))) s -= 1.5f;
            s += 0.4f * Vector2.Dot(dir, dribbleDir);
            if (brain.IsInGoalZone(probe, brain.M(0.6f))) s -= 3f;

            if (s > bestScore) { bestScore = s; best = dir; }
        }

        dribbleDir = best;
        me.MoveInput = best;

        float dGoal = Vector2.Dot(goal - pos, brain.AttackDir);
        me.SprintInput = oppDist > brain.M(4f) && dGoal > brain.M(10f) &&
                         me.Stamina01 > sprintMinStamina && !me.IsExhausted;

        Skater near = NearestOpponent(pos);
        Vector2 aimDir = best;
        if (near != null && oppDist < brain.M(2.5f))
        {
            Vector2 away = (pos - (Vector2)near.transform.position).normalized;
            aimDir = Vector2.Lerp(best, away, 0.6f).normalized;
        }
        me.AimDir = aimDir;
    }

    // Destino de la conducción:
    //  - Detrás de la línea de gol: salir por el lado (no meterse contra la red).
    //  - Cerca de portería o con urgencia alta: directo a la zona de remate, por el lado que el portero no cubre.
    //  - Lejos: el carril más libre que acabe en buena zona.
    Vector2 DribbleTarget(Vector2 pos, Vector2 goal, float urgency)
    {
        Vector2 atk = brain.AttackDir;
        Vector2 lat = brain.Lateral;
        float along = Vector2.Dot(goal - pos, atk);

        if (along < brain.M(1.5f))
        {
            float side = Vector2.Dot(pos - goal, lat) >= 0f ? 1f : -1f;
            return brain.Sanitize(goal - atk * brain.M(3.5f) + lat * side * brain.M(2.5f));
        }

        if (along < brain.M(11f) || urgency > 0.6f)
        {
            Vector2 best = brain.Sanitize(goal - atk * brain.M(4.5f));
            float bestQ = -1f;
            for (int k = -1; k <= 1; k++)
            {
                Vector2 p = brain.Sanitize(goal - atk * brain.M(4.5f) + lat * (k * brain.M(2f)));
                Vector2 aim;
                float qq = brain.ShotQuality(p, me.shootForce, out aim) +
                           0.3f * Mathf.Clamp01(tm.DistanceToNearestOpponent(p) / brain.M(3f));
                if (qq > bestQ) { bestQ = qq; best = p; }
            }
            return best;
        }

        float bestScore = float.MinValue;
        Vector2 bestP = brain.Sanitize(goal - atk * brain.M(4f));
        int bestLane = 0;

        for (int k = -2; k <= 2; k++)
        {
            Vector2 p = brain.Sanitize(pos + atk * brain.M(8f) + lat * (k * brain.M(3.5f)));

            float s = 1.5f * Mathf.Clamp01(tm.DistanceToNearestOpponent(p) / brain.M(6f));
            s += 1.5f * brain.LaneClear(pos, p);
            s += 1.0f * brain.Threat(p);
            s -= 0.15f * Mathf.Abs(k);
            if (k == lastLane) s += 0.4f;

            if (s > bestScore) { bestScore = s; bestP = p; bestLane = k; }
        }

        lastLane = bestLane;
        return bestP;
    }

    // =====================================================================
    //  Utilidades
    // =====================================================================

    Skater NearestOpponent(Vector2 p)
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

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // Movimiento hacia un punto rodeando porterías, con frenada al llegar y separación con otros jugadores.
    // La frenada se calcula con la distancia al DESTINO, no a la esquina intermedia (no frena en cada esquina).
    void GoTo(Vector2 target, float slowRadius, bool avoidOpponents, Vector2 feedForward)
    {
        Vector2 pos = transform.position;
        Vector2 waypoint = GoalNav.Route(pos, target, BodyClearance);

        float d = Vector2.Distance(pos, target);
        Vector2 toW = waypoint - pos;
        Vector2 desire = d < 0.2f || toW.sqrMagnitude < 0.0001f
            ? Vector2.zero
            : toW.normalized * Mathf.Clamp01(d / Mathf.Max(slowRadius, 0.01f));

        float r = brain.M(1.8f);
        Vector2 push = Vector2.zero;
        for (int i = 0; i < Skater.All.Count; i++)
        {
            Skater s = Skater.All[i];
            if (s == me) continue;
            if (!avoidOpponents && s.team != me.team) continue;

            Vector2 away = pos - (Vector2)s.transform.position;
            float dist = away.magnitude;
            if (dist > 0.01f && dist < r) push += away / dist * (1f - dist / r);
        }

        me.MoveInput = Vector2.ClampMagnitude(desire + feedForward + push * 1.2f, 1f);
    }

    // Amarillo = hacia dónde quería tirar, rojo = dirección real de salida.
    // Si coinciden y el tiro va fuera, el punto de mira está mal; si no coinciden, algo desvía la bola.
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || Time.time > dbgUntil) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(dbgFrom, dbgAim);
        Gizmos.DrawWireSphere(dbgAim, 0.15f);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(dbgFrom, dbgFrom + dbgDir * Vector2.Distance(dbgFrom, dbgAim) * 1.3f);
    }
}