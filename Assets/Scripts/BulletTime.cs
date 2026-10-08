using UnityEngine;

// Cámara lenta mientras el jugador apunta un tiro (mantener clic izquierdo).
// Lo añade solo el TeamManager humano. Dibuja la línea de apuntado (con el primer rebote en banda)
// y gestiona una barra de energía para que no se pueda abusar.
public class BulletTime : MonoBehaviour
{
    public static BulletTime Instance { get; private set; }

    [Header("Bullet time")]
    [Range(0.05f, 1f)] public float slowScale = 0.15f;  // velocidad del juego mientras apuntas
    public float transitionSpeed = 12f;                 // lo rápido que entra/sale (escala por segundo real)
    public float maxAimTime = 1.5f;                     // segundos REALES de apuntado con la barra llena
    public float regenPerSecond = 0.6f;                 // energía que se recupera por segundo real
    public float minEnergyToStart = 0.3f;               // energía mínima para poder activarlo

    [Header("Línea de apuntado")]
    public float lineLength = 17f;                      // alcance aproximado del tiro
    public float bounceKeep = 0.5f;                     // fracción de recorrido que se dibuja tras el rebote
    public float lineWidth = 0.08f;
    public Color lineColor = new Color(1f, 0.9f, 0.2f, 0.9f);

    public bool Active { get; private set; }

    float energy;
    float baseFixedDelta;
    bool owns;              // true mientras nosotros controlamos Time.timeScale
    bool aimThisFrame;
    LineRenderer line;

    void Awake()
    {
        Instance = this;
        baseFixedDelta = Time.fixedDeltaTime;
        energy = maxAimTime;
        CreateLine();
    }

    void OnDisable()
    {
        RestoreTime();
    }

    void OnDestroy()
    {
        RestoreTime();
    }

    // ---------- API ----------

    // Intenta activar el bullet time. Si no hay energía, no hace nada (el tiro sigue funcionando).
    public bool Begin()
    {
        if (energy < minEnergyToStart) return false;
        Active = true;
        owns = true;
        return true;
    }

    public void End()
    {
        Active = false;
    }

    // Se llama cada frame mientras se apunta: dibuja la línea desde 'origin' en dirección 'dir'
    public void SetAim(Vector2 origin, Vector2 dir, LayerMask walls, float length = -1f)
    {
        float len = length > 0f ? length : lineLength;
        Vector2 p1 = origin + dir * len;
        Vector2 p2 = p1;

        RaycastHit2D hit = Physics2D.Raycast(origin, dir, len, walls);
        if (hit.collider != null)
        {
            p1 = hit.point;
            float remaining = (len - hit.distance) * bounceKeep;
            p2 = p1 + Vector2.Reflect(dir, hit.normal) * remaining;
        }

        line.SetPosition(0, origin);
        line.SetPosition(1, p1);
        line.SetPosition(2, p2);
        aimThisFrame = true;
    }

    // ---------- Tiempo ----------

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        if (Active)
        {
            energy -= dt;
            if (energy <= 0f)
            {
                energy = 0f;
                Active = false;   // se acabó la cámara lenta; el tiro sigue pendiente hasta soltar
            }
        }
        else
        {
            energy = Mathf.Min(maxAimTime, energy + regenPerSecond * dt);
        }

        if (!owns) return;

        float target = Active ? slowScale : 1f;
        float scale = Mathf.MoveTowards(Time.timeScale, target, transitionSpeed * dt);
        ApplyScale(scale);

        if (!Active && Mathf.Approximately(scale, 1f)) owns = false;
    }

    void LateUpdate()
    {
        line.enabled = aimThisFrame;
        aimThisFrame = false;
    }

    // La física debe seguir a 50 pasos por segundo REALES, por eso se escala también fixedDeltaTime
    void ApplyScale(float scale)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDelta * scale;
    }

    void RestoreTime()
    {
        if (!owns) return;
        ApplyScale(1f);
        owns = false;
        Active = false;
    }

    // ---------- Visual ----------

    void CreateLine()
    {
        GameObject go = new GameObject("AimLine");
        go.transform.SetParent(transform, false);

        line = go.AddComponent<LineRenderer>();
        line.positionCount = 3;
        line.useWorldSpace = true;
        line.widthMultiplier = lineWidth;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = lineColor;
        line.endColor = new Color(lineColor.r, lineColor.g, lineColor.b, 0.15f);
        line.sortingOrder = 50;
        line.enabled = false;
    }

    void OnGUI()
    {
        if (!Active && energy >= maxAimTime - 0.01f) return;   // barra llena y sin usar: oculta

        float w = 220f, h = 14f;
        Rect r = new Rect((Screen.width - w) * 0.5f, Screen.height - 40f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        GUI.color = Active ? lineColor : Color.white;
        GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, (w - 4f) * Mathf.Clamp01(energy / maxAimTime), h - 4f),
                        Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}