using UnityEngine;

// Quick Time Event del portero del humano. Lo activa Goalkeeper con cada TIRO rival (uno por tiro):
// ralentiza el tiempo y muestra, en forma de media luna alrededor del portero (GoalkeeperQTEMeter, estilo
// NBA 2K), una franja que se llena y una zona verde: no hace falta apartar la vista de la jugada.
// Pulsar la tecla con la línea dentro de la zona verde = parada segura. Con el dedo (InputMode táctil)
// se pulsa el botón verde ATURA! que aparece en el sitio de XUTA.
// Fallar o no pulsar = el portero reacciona solo, pero más tarde y leyendo peor el tiro.
// Ponlo SOLO en el portero del equipo humano (en el rival se ignora igualmente).
[RequireComponent(typeof(Goalkeeper))]
public class GoalkeeperQTE : MonoBehaviour
{
    [Header("Ritmo")]
    [SerializeField, Range(0.02f, 0.5f)] float slowTimeScale = 0.06f;   // velocidad del juego durante el QTE
    [SerializeField] float barTime = 0.9f;                              // segundos REALES que tarda la línea en cruzar
    [SerializeField] KeyCode key = KeyCode.Space;
    [SerializeField] float inputGrace = 0.25f;                    // s reales al inicio en que se ignora la tecla
    [SerializeField, Range(0.3f, 1f)] float arriveMargin = 0.75f; // la barra acaba antes de que llegue la bola

    [Header("Dificultad")]
    [SerializeField, Range(0.05f, 0.6f)] float greenWidthFar = 0.28f;   // anchura de la zona verde (fracción) en tiros lejanos
    [SerializeField, Range(0.05f, 0.6f)] float greenWidthNear = 0.14f;  // ... en tiros a bocajarro
    [SerializeField] float nearDistance = 3f;
    [SerializeField] float farDistance = 12f;

    [SerializeField, Range(1f, 2.5f)] float streakGreenBonus = 1.5f;   // zona verda x això amb el porter en ratxa

    [Header("Indicador")]
    [SerializeField] float resultShowTime = 0.5f;


    [SerializeField] bool debugLog = true;   // escribe en la consola la cámara lenta de cada QTE
    float appliedScale = 1f, appliedFixed = 0.02f;

    public QTEState State { get; private set; } = QTEState.Idle;

    // TeamManager lo usa para no procesar Espacio durante el QTE (ni el frame en que se resuelve)
    static GoalkeeperQTE running;
    static int resolvedFrame = -1;
    public static bool BlocksInput => running != null || resolvedFrame == Time.frameCount;

    // Goal lo usa: con un QTE abierto no puede haber gol (la bola espera en la línea)
    public static bool ProtectsGoal => running != null;

    // Hay un QTE esperando la pulsación (TouchControls enseña entonces el botón verde ATURA!)
    public static bool IsOpen => running != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { running = null; resolvedFrame = -1; }

    float startTime, cursor, greenCenter, greenWidth;
    float savedTimeScale = 1f, savedFixedDelta = 0.02f;
    float showUntil;
    bool lastOk;
    GoalkeeperQTEMeter meter;
    Ball ball;

    // Lo que dibuja GoalkeeperQTEMeter (recorrido de 0 a 1)
    public float Cursor01 => cursor;
    public float GreenStart => greenCenter - greenWidth * 0.5f;
    public float GreenWidth => greenWidth;

    // Porter en ratxa (0..1, el posa Goalkeeper abans de cada QTE): la zona verda creix fins a x streakGreenBonus
    public float Assist { get; set; }
    public bool LastOk => lastOk;
    public bool ResultVisible => State != QTEState.Running && Time.unscaledTime < showUntil;
    public string KeyName => key == KeyCode.Space ? "ESPAI" : key.ToString().ToUpperInvariant();

    void Awake()
    {
        meter = GetComponent<GoalkeeperQTEMeter>();
        if (meter == null) meter = gameObject.AddComponent<GoalkeeperQTEMeter>();
    }

    public void Begin(float shotDistance, float timeToGoal)
    {
        if (State == QTEState.Running) return;

        float t = Mathf.InverseLerp(nearDistance, farDistance, shotDistance);
        greenWidth = Mathf.Lerp(greenWidthNear, greenWidthFar, t);
        greenWidth = Mathf.Min(0.6f, greenWidth * Mathf.Lerp(1f, streakGreenBonus, Assist));
        float half = greenWidth * 0.5f;
        greenCenter = Random.Range(Mathf.Max(0.3f, half + 0.05f), 1f - half - 0.03f);   // nunca pegada al inicio

        startTime = Time.unscaledTime;
        cursor = 0f;
        State = QTEState.Running;
        running = this;

        // La mitja lluna s'obre cap a d'on ve la bola
        if (ball == null) ball = FindFirstObjectByType<Ball>();
        Vector2 toShot = ball != null ? (Vector2)(ball.transform.position - transform.position) : Vector2.right;
        if (meter != null) meter.Orient(toShot);

        // Si el xut acaba de congelar el joc (HitStop) en aquest mateix pas de física, el temps a guardar
        // és el d'abans de congelar: si no, en acabar el QTE el joc es quedaria aturat (timeScale 0)
        savedTimeScale = HitStop.TakeOver();
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

        float elapsed = Time.unscaledTime - startTime;
        cursor = elapsed / Mathf.Max(barTime, 0.1f);
        bool pressed = Pressed();   // se lee siempre: una pulsación durante el margen inicial se descarta
        if (elapsed >= inputGrace && pressed)
        {
            bool ok = Mathf.Abs(cursor - greenCenter) <= greenWidth * 0.5f;
            if (debugLog) Debug.Log($"QTE {(ok ? "ACIERTO" : "FALLO")}: pulsado en {cursor:P0} (zona verde {greenCenter - greenWidth * 0.5f:P0}-{greenCenter + greenWidth * 0.5f:P0})");
            Resolve(ok);
        }
        else if (cursor >= 1f)
        {
            if (debugLog) Debug.Log("QTE FALLO: no se ha pulsado a tiempo");
            Resolve(false);
        }
    }

    // Tecla o, en modo táctil, el botón verde ATURA! que TouchControls pone en el sitio de XUTA
    bool Pressed()
    {
        if (TouchControls.QtePressed()) return true;
        return Input.GetKeyDown(key);
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
}

public enum QTEState { Idle, Running, Success, Fail }