using UnityEngine;

// Quick Time Event del portero del humano. Lo activa Goalkeeper al detectar un tiro rival:
// ralentiza el tiempo y muestra una barra con una zona verde y una línea que la recorre.
// Pulsar la tecla con la línea dentro de la zona verde = parada segura. Fallar o no pulsar = plantado.
// Ponlo SOLO en el portero del equipo humano.
[RequireComponent(typeof(Goalkeeper))]
public class GoalkeeperQTE : MonoBehaviour
{
    [Header("Ritmo")]
    [SerializeField, Range(0.02f, 0.5f)] float slowTimeScale = 0.06f;   // velocidad del juego durante el QTE
    [SerializeField] float barTime = 0.9f;                              // segundos REALES que tarda la línea en cruzar
    [SerializeField] KeyCode key = KeyCode.Space;
    [SerializeField] float inputGrace = 0.15f;                    // s reales al inicio en que se ignora la tecla
    [SerializeField, Range(0.3f, 1f)] float arriveMargin = 0.75f; // la barra acaba antes de que llegue la bola

    [Header("Dificultad")]
    [SerializeField, Range(0.05f, 0.6f)] float greenWidthFar = 0.28f;   // anchura de la zona verde (fracción) en tiros lejanos
    [SerializeField, Range(0.05f, 0.6f)] float greenWidthNear = 0.14f;  // ... en tiros a bocajarro
    [SerializeField] float nearDistance = 3f;
    [SerializeField] float farDistance = 12f;

    [Header("Barra")]
    [SerializeField] Vector2 barSize = new Vector2(320f, 22f);
    [SerializeField] float resultShowTime = 0.6f;


    [SerializeField] bool debugLog = true;   // escribe en la consola la cámara lenta de cada QTE
    float appliedScale = 1f, appliedFixed = 0.02f;

    public QTEState State { get; private set; } = QTEState.Idle;

    // TeamManager lo usa para no procesar Espacio durante el QTE (ni el frame en que se resuelve)
    static GoalkeeperQTE running;
    static int resolvedFrame = -1;
    public static bool BlocksInput => running != null || resolvedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { running = null; resolvedFrame = -1; }

    float startTime, cursor, greenCenter, greenWidth;
    float savedTimeScale = 1f, savedFixedDelta = 0.02f;
    float showUntil;
    bool lastOk;
    GUIStyle labelStyle;

    public void Begin(float shotDistance, float timeToGoal)
    {
        if (State == QTEState.Running) return;

        float t = Mathf.InverseLerp(nearDistance, farDistance, shotDistance);
        greenWidth = Mathf.Lerp(greenWidthNear, greenWidthFar, t);
        float half = greenWidth * 0.5f;
        greenCenter = Random.Range(Mathf.Max(0.3f, half + 0.05f), 1f - half - 0.03f);   // nunca pegada al inicio

        startTime = Time.unscaledTime;
        cursor = 0f;
        State = QTEState.Running;
        running = this;

        savedTimeScale = Time.timeScale;
        savedFixedDelta = Time.fixedDeltaTime;
        float baseFixed = savedFixedDelta / Mathf.Max(savedTimeScale, 0.01f);

        // Cámara lenta suficiente para que la barra termine ANTES de que llegue la bola
        float needed = timeToGoal * arriveMargin / Mathf.Max(barTime, 0.1f);
        float scale = Mathf.Clamp(Mathf.Min(slowTimeScale, needed), 0.01f, 1f);
        appliedScale = scale;
        appliedFixed = baseFixed * scale;
        Time.timeScale = appliedScale;
        Time.fixedDeltaTime = appliedFixed;

        if (debugLog) Debug.Log($"QTE en {name}: la bola llega en {timeToGoal:F2}s, cámara lenta x{scale:F3}");
    }

    void Update()
    {
        if (State != QTEState.Running) return;

        // Nadie nos quita la cámara lenta mientras dura el QTE
        if (!Mathf.Approximately(Time.timeScale, appliedScale))
        {
            Time.timeScale = appliedScale;
            Time.fixedDeltaTime = appliedFixed;
        }

        cursor = (Time.unscaledTime - startTime) / Mathf.Max(barTime, 0.1f);
        float elapsed = Time.unscaledTime - startTime;
        cursor = elapsed / Mathf.Max(barTime, 0.1f);
        if (elapsed >= inputGrace && Input.GetKeyDown(key)) Resolve(Mathf.Abs(cursor - greenCenter) <= greenWidth * 0.5f);
        else if (cursor >= 1f) Resolve(false);
    }

    void Resolve(bool ok)
    {
        State = ok ? QTEState.Success : QTEState.Fail;
        resolvedFrame = Time.frameCount;
        lastOk = ok;
        showUntil = Time.unscaledTime + resultShowTime;
        RestoreTime();
    }

    // La llama Goalkeeper cuando el tiro termina (parada, gol, desvío o reinicio)
    public void Cancel()
    {
        if (State == QTEState.Running) RestoreTime();
        State = QTEState.Idle;
    }

    void RestoreTime()
    {
        if (running != this) return;
        Time.timeScale = savedTimeScale;
        Time.fixedDeltaTime = savedFixedDelta;
        running = null;
    }

    void OnDisable() { Cancel(); }

    void OnGUI()
    {
        if (State != QTEState.Running && Time.unscaledTime >= showUntil) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;
        }

        Rect bar = new Rect((Screen.width - barSize.x) * 0.5f, Screen.height * 0.7f, barSize.x, barSize.y);
        Fill(bar, new Color(0f, 0f, 0f, 0.75f));

        Rect inner = new Rect(bar.x + 2f, bar.y + 2f, bar.width - 4f, bar.height - 4f);
        Fill(inner, new Color(0.25f, 0.25f, 0.3f, 0.95f));

        float gx = inner.x + (greenCenter - greenWidth * 0.5f) * inner.width;
        Fill(new Rect(gx, inner.y, greenWidth * inner.width, inner.height), new Color(0.3f, 0.9f, 0.4f));

        float cx = inner.x + Mathf.Clamp01(cursor) * inner.width;
        Fill(new Rect(cx - 2f, bar.y - 4f, 4f, bar.height + 8f), Color.white);

        string text = State == QTEState.Running ? "¡PARADA! [" + key + "]" : (lastOk ? "¡PARADÓN!" : "¡FALLO!");
        GUI.color = State == QTEState.Running ? Color.white : (lastOk ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f));
        GUI.Label(new Rect(bar.x, bar.y - 28f, bar.width, 24f), text, labelStyle);
        GUI.color = Color.white;
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

public enum QTEState { Idle, Running, Success, Fail }