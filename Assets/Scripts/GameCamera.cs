using UnityEngine;

// Cámara estilo retransmisión (como el NBA 2K): sigue la bola con suavidad, se adelanta hacia donde va el juego,
// hace zoom según la velocidad de la bola, mantiene a la vista al jugador controlado y no se sale de la pista.
// Se añade sola a la cámara principal (ortográfica); si prefieres ajustarla en el inspector, añádela tú a la cámara.
[RequireComponent(typeof(Camera))]
public class GameCamera : MonoBehaviour
{
    static GameCamera instance;

    [Header("Zoom")]
    public float baseSize = 8f;          // tamaño ortográfico en reposo (menor = más cerca; la pista ya no cabe entera)
    public float maxSize = 12f;          // tamaño máximo
    public float speedZoom = 2.5f;       // zoom-out extra con la bola a máxima velocidad
    public float sizeSmooth = 3f;
    public float viewMargin = 3f;        // espacio extra para ver al jugador controlado y la bola a la vez

    [Header("Seguimiento")]
    public float followSmooth = 4.5f;    // mayor = más pegada a la acción
    [Range(0f, 1f)] public float ballWeight = 0.7f;   // peso de la bola frente al jugador controlado
    public float lookAheadTime = 0.35f;  // anticipación según la velocidad de la bola
    public float maxLookAhead = 5f;
    public float attackBias = 2.5f;      // con la bola en tu equipo, se adelanta hacia la portería rival
    public Vector2 deadZone = new Vector2(1.2f, 0.7f);    // zona central donde la cámara no se mueve

    [Header("Límites")]
    public bool clampToRink = true;
    public float rinkPadding = 1.5f;     // cuánto puede asomar fuera de la pista

    [Header("Tiempo")]
    public bool useUnscaledTime = true;  // para que el bullet time no deje la cámara arrastrándose

    Camera cam;
    TeamManager human;
    float nextFind;
    bool initialized;
    Vector2 smoothPos;

    float shakeAmp, shakeDur, shakeStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Camera c = Camera.main;
        if (c == null || !c.orthographic) return;
        if (c.GetComponent<GameCamera>() == null) c.gameObject.AddComponent<GameCamera>();
    }

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
    }

    // Sacudida de cámara (tiros, goles...). Se puede llamar desde cualquier script.
    public static void Shake(float amplitude, float duration)
    {
        if (instance == null) return;

        float elapsed = Time.unscaledTime - instance.shakeStart;
        float remaining = instance.shakeDur > 0f ? instance.shakeAmp * Mathf.Clamp01(1f - elapsed / instance.shakeDur) : 0f;
        if (amplitude < remaining) return;   // no pisa una sacudida más fuerte en curso

        instance.shakeAmp = amplitude;
        instance.shakeDur = Mathf.Max(0.01f, duration);
        instance.shakeStart = Time.unscaledTime;
    }

    void FindHuman()
    {
        if (human != null || Time.unscaledTime < nextFind) return;
        nextFind = Time.unscaledTime + 1f;

        foreach (TeamManager t in FindObjectsByType<TeamManager>(FindObjectsSortMode.None))
        {
            if (t.humanControlled) { human = t; break; }
        }
    }

    void LateUpdate()
    {
        if (!cam.orthographic) return;

        Ball ball = Ball.Instance;
        if (ball == null) return;

        FindHuman();

        float dt = Mathf.Min(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime, 0.05f);

        // ---------- A dónde mirar ----------
        Vector2 ballPos = ball.rb.position;
        Vector2 ballVel = (ball.IsHeld && ball.Holder != null) ? ball.Holder.Velocity : ball.rb.linearVelocity;
        Vector2 focus = ballPos + Vector2.ClampMagnitude(ballVel * lookAheadTime, maxLookAhead);

        Skater ctrl = human != null ? human.Controlled : null;
        if (ctrl != null)
            focus = Vector2.Lerp((Vector2)ctrl.transform.position, focus, ballWeight);

        // Con la bola en nuestro equipo, la cámara se adelanta hacia la portería rival (más espacio por delante)
        if (human != null && human.ownGoal != null && human.opponentGoal != null &&
            ball.IsHeld && ball.Holder.team == human.team)
        {
            Vector2 atk = ((Vector2)human.opponentGoal.position - (Vector2)human.ownGoal.position).normalized;
            focus += atk * attackBias;
        }

        // ---------- Zoom ----------
        float speed = ball.rb.linearVelocity.magnitude;
        float desiredSize = baseSize + speedZoom * Mathf.Clamp01(speed / 25f);

        if (ctrl != null)
        {
            // Que se vean a la vez el jugador controlado y la bola
            Vector2 d = (Vector2)ctrl.transform.position - ballPos;
            float needH = Mathf.Abs(d.y) * 0.5f + viewMargin;
            float needW = (Mathf.Abs(d.x) * 0.5f + viewMargin) / cam.aspect;
            desiredSize = Mathf.Max(desiredSize, needH, needW);
        }
        desiredSize = Mathf.Clamp(desiredSize, baseSize, maxSize);

        if (!initialized)
        {
            smoothPos = focus;
            cam.orthographicSize = desiredSize;
            initialized = true;
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, desiredSize, 1f - Mathf.Exp(-sizeSmooth * dt));

        // ---------- Posición con zona muerta ----------
        Vector2 delta = focus - smoothPos;
        delta.x = ApplyDeadZone(delta.x, deadZone.x);
        delta.y = ApplyDeadZone(delta.y, deadZone.y);
        smoothPos += delta * (1f - Mathf.Exp(-followSmooth * dt));

        if (clampToRink) smoothPos = ClampToRink(smoothPos);

        // ---------- Sacudida ----------
        Vector2 shake = Vector2.zero;
        if (shakeDur > 0f)
        {
            float t = (Time.unscaledTime - shakeStart) / shakeDur;
            if (t < 1f) shake = Random.insideUnitCircle * shakeAmp * (1f - t) * (1f - t);
        }

        transform.position = new Vector3(smoothPos.x + shake.x, smoothPos.y + shake.y, transform.position.z);
    }

    static float ApplyDeadZone(float v, float zone)
    {
        float a = Mathf.Abs(v);
        return a <= zone ? 0f : Mathf.Sign(v) * (a - zone);
    }

    // Mantiene la vista dentro de la pista (si la pista es más pequeña que la vista, centra)
    Vector2 ClampToRink(Vector2 p)
    {
        Bounds b;
        if (!Rink.TryGetBounds(out b)) return p;

        float hh = cam.orthographicSize;
        float hw = hh * cam.aspect;

        float minX = b.min.x + hw - rinkPadding, maxX = b.max.x - hw + rinkPadding;
        float minY = b.min.y + hh - rinkPadding, maxY = b.max.y - hh + rinkPadding;

        p.x = minX > maxX ? b.center.x : Mathf.Clamp(p.x, minX, maxX);
        p.y = minY > maxY ? b.center.y : Mathf.Clamp(p.y, minY, maxY);
        return p;
    }
}
