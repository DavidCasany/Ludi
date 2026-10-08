using UnityEngine;

// Ejecuta lo que decide el TeamBrain:
//  - Sin bola: va a la posición de su rol (cazador, cobertura, marcaje, rematador, extremo, seguridad).
//  - Con bola: evalúa tirar, pasar o conducir comparando la calidad de cada opción.
[RequireComponent(typeof(Skater))]
public class SkaterAI : MonoBehaviour
{
    [Header("Nivel de la IA")]
    [Range(0f, 1f)] public float skill = 0.7f;   // afecta a la puntería y al tiempo de reacción

    [Header("Decisiones con bola")]
    public float thinkTime = 0.3f;               // pausa antes de decidir al recibir la bola
    public float pressureDistance = 2f;          // rival a menos de esto = presionado (metros)
    public float ownZoneDistance = 12f;          // "mi zona" para despejar (metros)
    public float passMargin = 0.08f;             // cuánto mejor debe ser un pase que seguir conduciendo
    public float maxHoldTime = 3.5f;             // pasado este tiempo fuerza una acción

    [Header("Tiro")]
    public float shootThreshold = 0.2f;          // calidad mínima de tiro para chutar
    public float pressuredShootThreshold = 0.12f;// ídem si le presionan (chuta antes)
    public float shootPreference = 0.12f;        // ventaja que da a tirar él frente al tiro de un compañero
    public float shootZone = 10f;                // zona de remate (metros)
    public float shootPatience = 1f;             // en zona de remate, tras este tiempo con la bola tira igualmente

    [Header("Saque de centro")]
    public float serveDelay = 3f;

    [Header("Debug")]
    public bool debugLog = false;                // imprime los valores de decisión en zona de remate

    Skater me;
    TeamManager tm;
    TeamBrain brain;
    bool hadBall;
    float ballSince;
    float nextDecision;
    float nextLog;
    Vector2 dribbleDir = Vector2.right;

    void Awake()
    {
        me = GetComponent<Skater>();
        tm = GetComponentInParent<TeamManager>();

        if (tm != null)
        {
            brain = tm.GetComponent<TeamBrain>();
            if (brain == null) brain = tm.gameObject.AddComponent<TeamBrain>();
        }
    }

    void FixedUpdate()
    {
        Ball ball = Ball.Instance;
        if (ball == null || tm == null || brain == null) return;

        if (me.HasBall && !hadBall) ballSince = Time.time;
        hadBall = me.HasBall;

        // Saque de centro: espera un poco y pasa al mejor compañero
        GameManager gm = GameManager.Instance;
        if (me.HasBall && gm != null && gm.IsServing(me))
        {
            me.MoveInput = Vector2.zero;
            if (Time.time - ballSince > serveDelay) me.PassTo(PickServeTarget());
            return;
        }

        if (me.HasBall)
        {
            if (Time.time >= nextDecision)
            {
                nextDecision = Time.time + 0.1f + Random.Range(0f, 0.04f); // desincroniza a los jugadores
                DecideWithBall();
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

        float slow = a.role == AIRole.Hunter ? 0.01f : brain.M(1.5f);
        GoTo(a.target, slow);

        Vector2 toBall = (Vector2)ball.transform.position - (Vector2)transform.position;
        if (toBall.sqrMagnitude > 0.01f) me.AimDir = toBall.normalized;
    }

    // =====================================================================
    //  CON BOLA
    // =====================================================================

    void DecideWithBall()
    {
        Vector2 pos = transform.position;
        Vector2 goal = tm.opponentGoal.position;
        float held = Time.time - ballSince;

        Vector2 aimPoint;
        float q = brain.ShotQuality(pos, out aimPoint);
        float oppDist = tm.DistanceToNearestOpponent(pos);
        bool pressured = oppDist < brain.M(pressureDistance);

        bool inZone = Vector2.Distance(pos, goal) < brain.M(shootZone) &&
                      Vector2.Dot(goal - pos, brain.AttackDir) > brain.M(1f);

        float baseThink = thinkTime * Mathf.Lerp(1.5f, 0.7f, skill);
        float think = q > 0.35f ? baseThink * 0.4f : baseThink;   // con un buen tiro no se lo piensa
        bool thinking = held < think;

        float passValue, mateShot;
        Skater mate = BestPass(out passValue, out mateShot);

        float freedom = Mathf.Clamp01((oppDist - brain.M(1f)) / brain.M(3f));
        float holdValue = 0.1f + 0.45f * q + 0.3f * freedom;
        bool overheld = held > maxHoldTime;

        if (debugLog && inZone && Time.time >= nextLog)
        {
            nextLog = Time.time + 0.5f;
            Debug.Log($"{name}: q={q:F2} mateShot={mateShot:F2} pass={passValue:F2} hold={holdValue:F2} held={held:F1}s pressured={pressured}");
        }

        if (!thinking)
        {
            float thr = overheld ? 0.08f : (pressured ? pressuredShootThreshold : shootThreshold);

            // 1) Tiro: si es suficientemente bueno y ningún compañero tiene un tiro claramente mejor
            if (q >= thr && q + shootPreference >= mateShot)
            {
                ShootAt(aimPoint);
                return;
            }

            // 2) Tiro por paciencia: en zona de remate y con la bola un rato, tira igualmente
            if (inZone && held > shootPatience && q > 0.03f)
            {
                ShootAt(aimPoint);
                return;
            }

            // 3) Pase
            if (mate != null && (passValue > holdValue + passMargin || overheld))
            {
                me.PassTo(mate);
                return;
            }

            // 4) Despeje: presionado en mi zona y sin pase posible
            bool inOwnZone = Vector2.Distance(pos, tm.ownGoal.position) < brain.M(ownZoneDistance);
            if (pressured && inOwnZone && mate == null)
            {
                Clear();
                return;
            }
        }

        // 5) Conducir
        Dribble(pos, oppDist);
    }

    // Mejor compañero para un pase: línea libre, buen tiro desde su posición, progresa y está desmarcado.
    // mateShot = el mejor tiro (ya ponderado por la línea de pase) que tendría algún compañero.
    Skater BestPass(out float bestValue, out float mateShot)
    {
        Skater best = null;
        bestValue = 0f;
        mateShot = 0f;

        Vector2 pos = transform.position;
        Vector2 goal = tm.opponentGoal.position;
        float myDist = Vector2.Distance(pos, goal);

        foreach (Skater m in tm.skaters)
        {
            if (m == me) continue;

            Vector2 mp = m.transform.position;
            float dist = Vector2.Distance(pos, mp);
            if (dist < brain.M(2f)) continue;

            Vector2 lead = mp + m.Velocity * 0.4f;
            float lane = brain.LaneClear(pos, lead);
            if (lane < 0.35f) continue;

            Vector2 aim;
            float q = brain.ShotQuality(lead, out aim);
            float progress = Mathf.Clamp((myDist - Vector2.Distance(mp, goal)) / brain.M(10f), -0.3f, 1f);
            float open = Mathf.Clamp01(tm.DistanceToNearestOpponent(mp) / brain.M(4f));
            float range = dist > brain.M(20f) ? 0.5f : 1f;

            mateShot = Mathf.Max(mateShot, q * lane);

            float value = lane * range * (0.45f * q + 0.30f * progress + 0.25f * open);
            if (value > bestValue) { bestValue = value; best = m; }
        }
        return best;
    }

    Skater PickServeTarget()
    {
        float v, ms;
        Skater mate = BestPass(out v, out ms);
        if (mate != null) return mate;

        // Si ninguno cumple, el más cercano
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

    void ShootAt(Vector2 aimPoint)
    {
        Vector2 pos = transform.position;

        // Error de puntería según el nivel
        float noise = brain.M(Mathf.Lerp(0.5f, 0.05f, skill));
        aimPoint += brain.Lateral * Random.Range(-noise, noise);

        // Skater.Shoot suma parte de la velocidad del jugador: se compensa para clavar la dirección
        Vector2 desired = (aimPoint - pos).normalized * me.shootForce;
        Vector2 cmd = desired - me.Velocity * me.inheritVelocity;
        me.Shoot(cmd.normalized, cmd.magnitude);
    }

    // Despeje hacia delante y hacia la banda de su lado
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

    // Conducción: avanza hacia la portería esquivando rivales y paredes, busca líneas de tiro
    // y protege la bola con el cuerpo.
    void Dribble(Vector2 pos, float oppDist)
    {
        Vector2 goal = tm.opponentGoal.position;
        Vector2 target = goal - brain.AttackDir * brain.M(4f);
        Vector2 toT = target - pos;

        if (toT.sqrMagnitude < 0.04f)
        {
            me.MoveInput = Vector2.zero;
            return;
        }

        Vector2 dir0 = toT.normalized;
        Vector2 best = dir0;
        float bestScore = float.MinValue;

        for (int i = -3; i <= 3; i++)   // abanico de -60 a +60 grados
        {
            Vector2 dir = Rotate(dir0, i * 20f);
            Vector2 probe = pos + dir * brain.M(3.5f);

            float s = Vector2.Dot(dir, dir0);                                                    // ir hacia la portería
            s += 1.5f * Mathf.Clamp01(tm.DistanceToNearestOpponent(probe) / brain.M(3f));        // evitar rivales
            if (!brain.IsInsideRink(probe, brain.M(0.5f))) s -= 1.5f;                            // evitar paredes
            s += 0.4f * Vector2.Dot(dir, dribbleDir);                                            // estabilidad

            Vector2 aim;
            s += 2f * brain.ShotQuality(probe, out aim);                                         // buscar línea de tiro

            if (s > bestScore) { bestScore = s; best = dir; }
        }

        dribbleDir = best;
        me.MoveInput = best;

        // Proteger la bola: colocarla en el lado contrario al rival más cercano
        Skater near = NearestOpponent(pos);
        Vector2 aimDir = best;
        if (near != null && oppDist < brain.M(2.5f))
        {
            Vector2 away = (pos - (Vector2)near.transform.position).normalized;
            aimDir = Vector2.Lerp(best, away, 0.6f).normalized;
        }
        me.AimDir = aimDir;
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

    // slowRadius: a esa distancia del objetivo empieza a frenar (0.01 = a tope hasta llegar)
    void GoTo(Vector2 target, float slowRadius)
    {
        Vector2 to = target - (Vector2)transform.position;
        float d = to.magnitude;
        me.MoveInput = d < 0.2f ? Vector2.zero : to.normalized * Mathf.Clamp01(d / slowRadius);
    }
}