using UnityEngine;

// Quick Time Event del portero del humano. Lo activa Goalkeeper con cada TIRO rival (uno por tiro):
// ralentiza el tiempo y muestra una barra con una zona verde y una línea que la recorre.
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

    [Header("Barra")]
    [SerializeField] Vector2 barSize = new Vector2(320f, 22f);
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

        GUIScale.Apply();
        Rect bar = new Rect((GUIScale.Width - barSize.x) * 0.5f, GUIScale.Height * 0.7f, barSize.x, barSize.y);
        // Panell metàl·lic que envolta el text i la barra (estil de l'art, HudSkin)
        Rect panel = new Rect(bar.x - 22f, bar.y - 44f, bar.width + 44f, bar.height + 62f);

        // Resultat: la barra desapareix i queda només el veredicte, perquè no sembli que el QTE continua obert
        if (State != QTEState.Running)
        {
            Color resultColor = lastOk ? HudSkin.Green : HudSkin.Red;
            HudSkin.SteelPanel(panel);
            HudSkin.Bar(bar, 1f, resultColor);
            labelStyle.fontSize = 22;
            HudSkin.OutlinedLabel(new Rect(bar.x, bar.y - 38f, bar.width, 32f), lastOk ? "ATURADÀS!" : "FALLADA!", labelStyle, resultColor);
            labelStyle.fontSize = 16;
            return;
        }

        HudSkin.SteelPanel(panel);
        HudSkin.Bar(bar, 0f, Color.white);   // només el fons de la barra

        // Zona bona
        Rect inner = new Rect(bar.x + 2f, bar.y + 2f, bar.width - 4f, bar.height - 4f);
        float gx = inner.x + (greenCenter - greenWidth * 0.5f) * inner.width;
        HudSkin.Bar(new Rect(gx - 2f, bar.y, greenWidth * inner.width + 4f, bar.height), 1f, HudSkin.Green);

        // Cursor: pal crema amb contorn fosc
        float cx = inner.x + Mathf.Clamp01(cursor) * inner.width;
        Fill(new Rect(cx - 4f, bar.y - 7f, 8f, bar.height + 14f), HudSkin.Ink);
        Fill(new Rect(cx - 2f, bar.y - 5f, 4f, bar.height + 10f), HudSkin.Paper);

        labelStyle.fontSize = 18;
        string prompt = InputMode.IsTouch ? "ATURA! [botó verd]" : "ATURA! [" + key + "]";
        HudSkin.OutlinedLabel(new Rect(bar.x, bar.y - 36f, bar.width, 28f), prompt, labelStyle, HudSkin.Paper);
        labelStyle.fontSize = 16;
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

public enum QTEState { Idle, Running, Success, Fail }