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
//   Clic der. ...... pase hacia el cursor (se ajusta a un compañero cercano al cursor)
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

    [Header("Pase con el ratón")]
    public float passAssistRadius = 1.5f;  // si el cursor está a menos de esto de un compañero, el pase va a él

    [Header("HUD")]
    public bool showControlsHint = true;

    public Skater Controlled { get; private set; }

    Camera cam;
    float nextAutoSwitch;

    // Apuntado del tiro (bullet time)
    BulletTime bulletTime;
    bool aiming;
    Skater aimingSkater;

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

        if (Input.GetMouseButtonDown(1))   // pase hacia donde apunta el cursor
        {
            CancelAim();
            DoPass(mouse);
        }

        if (Input.GetKeyDown(KeyCode.Space))
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

            // Dirección desde la bola (no desde el centro del jugador) para apuntar con precisión
            Vector2 ballPos = (Vector2)Controlled.transform.position + Controlled.AimDir * Controlled.CurrentHoldDistance;
            Vector2 toTarget = mouse - ballPos;
            Vector2 shotDir = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : Controlled.AimDir;

            // Alcance aproximado del tiro: velocidad / rozamiento de la bola
            float range = Controlled.shootForce / Mathf.Max(Ball.Instance.rb.linearDamping, 0.01f);
            bulletTime.SetAim(ballPos, shotDir, Controlled.wallLayer, range);

            if (Input.GetMouseButtonUp(0))
            {
                bulletTime.End();
                aiming = false;
                Controlled.Shoot(shotDir, Controlled.shootForce);
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
            Controlled.Stick();
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

        Skater mate = MateNearPoint(mouse);
        if (mate != null) Controlled.PassTo(mate);
        else Controlled.PassToPoint(mouse);
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
                "WASD mover · Shift sprint · Clic izq: tirar (mantener) / stick sin bola · Clic der: pase · Espacio: regate / cambiar jugador",
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
}