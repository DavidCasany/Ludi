using System.Collections.Generic;
using UnityEngine;

public enum Team { Home, Away }

// Pon este script en un GameObject padre y cuelga de él los 4 Skaters y el Goalkeeper.
//
// CONTROLES DEL HUMANO
//   WASD ........... mover
//   Shift .......... sprint (gasta barra)
//   Clic izq. ...... con bola: mantener = bullet time + apuntar, soltar = tirar
//                    sin bola: golpe de stick (robar / interceptar hacia el cursor)
//   Clic der. ...... con bola: pase hacia el cursor (se ajusta a un compañero cercano al cursor)
//                    sin bola (mantener): defensa asistida (DefendAssist)
//   Espacio ........ con bola: regate | sin bola: cambiar al jugador más cercano a la bola
public class TeamManager : MonoBehaviour
{
    public Team team;
    public bool humanControlled = true;
    public TeamManager opponent;
    public Transform ownGoal;        // centro de la portería que defiende
    public Transform opponentGoal;   // centro de la portería rival

    public List<Skater> skaters = new List<Skater>();  // jugadores de campo (se autorrellena)
    public Goalkeeper goalkeeper;                      // se autorrellena

    [Header("Tiro asistido")]
    public bool shotAssist = true;
    [Tooltip("Si apuntas a menos de estos grados de una esquina de la portería, el tiro se ajusta a ella")]
    public float shotAssistAngle = 10f;

    TeamBrain brain;

    [Header("Pase con el ratón")]
    public float passAssistRadius = 1.5f;  // si el cursor está a menos de esto de un compañero, el pase va a él

    [Header("Cambio de jugador")]
    public float autoSwitchLockout = 0.6f;     // mínimo entre cambios automáticos de jugador (s)
    public float manualSwitchLockout = 1.5f;   // tras cambiar con Espacio, el automático espera esto (s)
    public float defendSwitchMargin = 5f;      // defendiendo: cambia si otro está esto mejor situado (m)

    [Header("Stick")]
    [Tooltip("El clic solo golpea si la bola está a menos de stickReach x esto")]
    public float stickClickRange = 1.1f;

    [Header("HUD")]
    public bool showControlsHint = true;

    public Skater Controlled { get; private set; }

    Camera cam;
    float nextAutoSwitch;
    Skater lastRivalCarrier;

    // Apuntado del tiro (bullet time)
    BulletTime bulletTime;
    bool aiming;
    Skater aimingSkater;

    // Defensa asistida (clic derecho mantenido sin bola)
    DefendAssist defendAssist;

    GUIStyle smallStyle;

    void Awake()
    {
        cam = Camera.main;
        if (skaters.Count == 0) skaters.AddRange(GetComponentsInChildren<Skater>());
        if (goalkeeper == null) goalkeeper = GetComponentInChildren<Goalkeeper>();

        foreach (Skater s in skaters) s.team = team;
        if (goalkeeper != null) goalkeeper.team = team;

        if (humanControlled)
        {
            bulletTime = GetComponent<BulletTime>();
            if (bulletTime == null) bulletTime = gameObject.AddComponent<BulletTime>();

            defendAssist = GetComponent<DefendAssist>();
            if (defendAssist == null) defendAssist = gameObject.AddComponent<DefendAssist>();
        }
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

    void OnDisable()
    {
        CancelAim();
    }

    bool CanShootNow => GameManager.Instance == null || GameManager.Instance.CanShoot;

    // ---------- Input del humano ----------

    void HandleInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        input = Vector2.ClampMagnitude(input, 1f);
        Controlled.MoveInput = input;
        Controlled.SprintInput = Input.GetKey(KeyCode.LeftShift);

        Vector2 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mouse - (Vector2)Controlled.transform.position;
        if (toMouse.sqrMagnitude > 0.001f) Controlled.AimDir = toMouse.normalized;

        HandleLeftClick(mouse);
        ShowPassPreview(mouse);

        if (Input.GetMouseButtonDown(1))   // pase hacia donde apunta el cursor
        {
            CancelAim();
            DoPass(mouse);
        }

        // Defensa asistida: mantener clic derecho sin bola
        if (!Controlled.HasBall && Input.GetMouseButton(1) && defendAssist != null)
            defendAssist.Tick(Controlled, input, ownGoal);

        if (Input.GetKeyDown(KeyCode.Space) && !GoalkeeperQTE.BlocksInput)
        {
            if (Controlled.HasBall)
            {
                Controlled.Juke(input);   // regate (sin dirección: corta hacia el lado libre del rival)
            }
            else
            {
                CancelAim();
                SwitchToClosestToBall();
            }
        }
    }

    // Clic izquierdo:
    //  - con bola: al mantenerlo se activa el bullet time y se apunta; al soltarlo se chuta
    //  - sin bola: golpe de stick hacia el cursor (robar o interceptar)
    void HandleLeftClick(Vector2 mouse)
    {
        if (aiming)
        {
            // Se cancela si cambia el jugador, pierde la bola o el juego no está en marcha
            if (Controlled != aimingSkater || !Controlled.HasBall || !CanShootNow)
            {
                CancelAim();
                return;
            }

            // Dirección desde la posición REAL de la bola para que el tiro salga hacia el cursor
            Vector2 ballPos = Ball.Instance.rb.position;
            Vector2 toTarget = mouse - ballPos;
            Vector2 shotDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Controlled.AimDir;
            shotDir = AssistShot(ballPos, shotDir);

            // Alcance aproximado del tiro: velocidad / rozamiento de la bola
            float range = Controlled.shootForce / Mathf.Max(Ball.Instance.rb.linearDamping, 0.01f);
            bulletTime.SetAim(ballPos, shotDir, Controlled.wallLayer, range);

            if (Input.GetMouseButtonUp(0))
            {
                bulletTime.End();
                aiming = false;
                Controlled.ShootExact(shotDir, Controlled.shootForce);   // sin desvío por la velocidad del patinador
            }
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;

        if (Controlled.HasBall)
        {
            if (!CanShootNow) return;
            aiming = true;
            aimingSkater = Controlled;
            bulletTime.Begin();   // si no hay energía no ralentiza, pero el tiro funciona igual
        }
        else
        {
            // Robo / intercepción: solo cuenta el clic. El golpe va siempre hacia la bola, el cursor no importa.
            // Si la bola está fuera de alcance no se golpea (antes un clic lejano dejaba descolocado y gastaba el cooldown).
            Ball b = Ball.Instance;
            if (b == null) return;

            Vector2 toBall = b.rb.position - (Vector2)Controlled.transform.position;
            if (toBall.magnitude > Controlled.stickReach * stickClickRange) return;

            if (toBall.sqrMagnitude > 0.0001f) Controlled.AimDir = toBall.normalized;
            Controlled.Stick();
        }
    }

    // Vista previa del pase asistido: a quién irá (y por dónde) si pulsas el clic derecho ahora
    void ShowPassPreview(Vector2 mouse)
    {
        if (aiming || !Controlled.HasBall) return;

        PassPlan plan;
        if (Controlled.PlanSmartPass(mouse, passAssistRadius, out plan))
        {
            Vector2 from = (Vector2)Controlled.transform.position + Controlled.AimDir * Controlled.CurrentHoldDistance;
            Feedback.ShowPassPreview(from, plan.bank, plan.waypoint, plan.mate.transform.position);
        }
    }

    void CancelAim()
    {
        if (!aiming) return;
        aiming = false;
        if (bulletTime != null) bulletTime.End();
    }

    // Pase al punto del cursor; si hay un compañero cerca del cursor, la bola va hacia él
    void DoPass(Vector2 mouse)
    {
        if (!Controlled.HasBall) return;

        // Asistido: compañero junto al cursor, en la dirección del cursor, o al que se llega rebotando en la banda
        Controlled.SmartPass(mouse, passAssistRadius);
    }

    Skater MateNearPoint(Vector2 point)
    {
        Skater best = null;
        float bestDist = passAssistRadius;
        foreach (Skater s in skaters)
        {
            if (s == Controlled) continue;
            float d = Vector2.Distance(s.transform.position, point);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }

    // ---------- Cambio de jugador ----------

        void AutoSelect()
    {
        Ball ball = Ball.Instance;
        if (ball == null) return;

        // 1) Siempre controlas a quien lleva la bola (si un compañero la recoge, saltas a él)
        if (ball.IsHeld && ball.Holder.team == team)
        {
            lastRivalCarrier = null;
            if (ball.Holder != Controlled)
            {
                SetControlled(ball.Holder);
                nextAutoSwitch = Time.time + autoSwitchLockout;
            }
            return;
        }

        // 2) El rival lleva la bola: controlas al defensor mejor situado
        if (ball.IsHeld)
        {
            Skater carrier = ball.Holder;
            bool newCarrier = carrier != lastRivalCarrier;
            lastRivalCarrier = carrier;

            if (Time.time < nextAutoSwitch || Input.GetMouseButton(1)) return;   // no cambiar en plena defensa asistida

            Skater best = BestDefender(carrier, null);
            if (best == null || best == Controlled) return;

            float margin = newCarrier ? 1.5f : defendSwitchMargin;   // al perder la bola, cambio casi inmediato
            if (DefenderScore(best, carrier) + margin < DefenderScore(Controlled, carrier))
            {
                SetControlled(best);
                nextAutoSwitch = Time.time + autoSwitchLockout;
            }
            return;
        }

        lastRivalCarrier = null;
        if (Time.time < nextAutoSwitch) return;

        Vector2 bp = ball.rb.position;
        Skater nearest = ClosestToBall();
        if (nearest == Controlled) return;

        float dNew = Vector2.Distance(nearest.transform.position, bp);
        float dCur = Vector2.Distance(Controlled.transform.position, bp);

        // 3) Pase de nuestro equipo en vuelo: pasas al receptor cuando la bola se le acerca
        // 4) Bola suelta cualquiera: solo si estás lejos de ella y otro está claramente más cerca
        bool ourPass = ball.IsPassFor(team);
        bool switchNow = ourPass ? dNew + 1f < dCur
                                 : dCur > 6f && dNew + 3f < dCur;

        if (switchNow)
        {
            SetControlled(nearest);
            nextAutoSwitch = Time.time + autoSwitchLockout;
        }
    }

    // Distancia "efectiva" de un defensor al portador: si está por detrás de él (más lejos de nuestra
    // portería que el portador), persiguiendo no llega nunca, así que se penaliza.
    float DefenderScore(Skater s, Skater carrier)
    {
        Vector2 sp = s.transform.position;
        Vector2 cp = carrier.transform.position;
        Vector2 goal = ownGoal != null ? (Vector2)ownGoal.position : sp;

        float d = Vector2.Distance(sp, cp);
        float behind = Vector2.Distance(sp, goal) - Vector2.Distance(cp, goal);
        if (behind > 0f) d += behind * 1.5f;
        return d;
    }

    Skater BestDefender(Skater carrier, Skater exclude)
    {
        Skater best = null;
        float bestScore = float.MaxValue;
        foreach (Skater s in skaters)
        {
            if (s == exclude) continue;
            float sc = DefenderScore(s, carrier);
            if (sc < bestScore) { bestScore = sc; best = s; }
        }
        return best;
    }

    // Espacio sin bola: defendiendo, al mejor defensor; si no, al más cercano a la bola
    void SwitchToClosestToBall()
    {
        Ball ball = Ball.Instance;
        if (ball == null) return;

        Skater best;
        if (ball.IsHeld && ball.Holder.team != team)
        {
            best = BestDefender(ball.Holder, Controlled);
        }
        else
        {
            best = null;
            float bestDist = float.MaxValue;
            foreach (Skater s in skaters)
            {
                if (s == Controlled) continue;
                float d = Vector2.Distance(s.transform.position, ball.transform.position);
                if (d < bestDist) { bestDist = d; best = s; }
            }
        }

        if (best != null)
        {
            SetControlled(best);
            nextAutoSwitch = Time.time + manualSwitchLockout;
        }
    }

    public void SetControlled(Skater s)
    {
        if (Controlled != null)
        {
            Controlled.SprintInput = false;
            Controlled.SetControlled(false);
        }
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

    public void ResetPositions()
    {
        CancelAim();
        foreach (Skater s in skaters) s.ResetToAnchor();
        if (goalkeeper != null) goalkeeper.ResetPosition();
    }

    public void SwapSides()
    {
        (ownGoal, opponentGoal) = (opponentGoal, ownGoal);

        foreach (Skater s in skaters) s.MirrorAnchor();
        if (goalkeeper != null) goalkeeper.SwapSide();
    }

    // ---------- HUD ----------

    void OnGUI()
    {
        if (!humanControlled || Controlled == null) return;

        if (smallStyle == null)
        {
            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.fontSize = 11;
        }

        float w = 220f;
        float x = (Screen.width - w) * 0.5f;

        // Barra de sprint (la de bullet time está justo debajo)
        Color sprintColor = Controlled.IsExhausted ? new Color(1f, 0.35f, 0.35f) : new Color(0.4f, 1f, 0.5f);
        DrawBar(new Rect(x, Screen.height - 62f, w, 10f), Controlled.Stamina01, sprintColor, "SPRINT");

        // Cooldowns (solo se ven mientras se recargan)
        float y = Screen.height - 76f;
        if (Controlled.JukeCooldown01 > 0f)
        {
            DrawBar(new Rect(x, y, w, 6f), 1f - Controlled.JukeCooldown01, new Color(1f, 0.9f, 0.3f), "REGATE");
            y -= 12f;
        }
        if (Controlled.StickCooldown01 > 0f)
        {
            DrawBar(new Rect(x, y, w, 6f), 1f - Controlled.StickCooldown01, Color.white, "STICK");
        }

        if (showControlsHint)
        {
            GUI.Label(new Rect(10f, Screen.height - 24f, 900f, 20f),
                "WASD mover · Shift sprint · Clic izq: tirar (mantener) / stick · Clic der: pase / defender (mantener sin bola) · Espacio: regate / cambiar",
                smallStyle);
        }
    }

    void DrawBar(Rect r, float fill, Color color, string label)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * Mathf.Clamp01(fill), r.height - 2f),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.xMax + 6f, r.y - 5f, 90f, 18f), label, smallStyle);
    }

        // Tiro asistido: si apuntas cerca de una esquina, el tiro va a esa esquina (como hace la IA)
    Vector2 AssistShot(Vector2 from, Vector2 dir)
    {
        if (!shotAssist) return dir;
        if (brain == null) brain = GetComponent<TeamBrain>();   // lo crea SkaterAI si falta
        if (brain == null) return dir;

        Vector2 goal = brain.OppGoalPos;
        Vector2 lat = brain.Lateral;
        float aimLat = Mathf.Max(0.05f, brain.OppGoalHalfWidth - brain.Profile.postMargin);

        Vector2 best = dir;
        float bestAng = shotAssistAngle;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 to = goal + lat * (s * aimLat) - from;
            if (to.sqrMagnitude < 0.01f) continue;
            float ang = Vector2.Angle(dir, to);
            if (ang < bestAng) { bestAng = ang; best = to.normalized; }
        }
        return best;
    }
}