using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Controles en pantalla para jugar con el dedo (móvil / tableta). Se construyen por código, no hace
// falta ningún prefab: TeamManager (equipo humano) llama a TouchControls.Ensure() al empezar.
//
//   Izquierda ....... joystick flotante (donde pongas el pulgar)           = WASD
//   Derecha:
//     XUTA / ROBA ... con bola: mantener = cámara lenta, arrastrar = apuntar, soltar = tirar
//                     (soltar sin arrastrar = tiro a portería). Sin bola: golpe de stick.
//     PASSA ......... con bola: pase hacia donde apunta el joystick (se ajusta al compañero).
//                     Sin bola no aparece.                                  = clic derecho
//     REGAT / CANVI . con bola: regate. Sin bola: cambiar de jugador.        = Espacio
//     SPRINT ........ mantener                                              = Shift
//   Arriba derecha .. pausa                                                 = Esc
//
// QTE del portero: mientras está abierto se esconde todo y solo queda un botón verde grande ATURA!
// en el sitio de XUTA (lo lee GoalkeeperQTE con TouchControls.QtePressed()).
//
// Solo se ven en modo táctil (InputMode) y mientras se juega (saque o juego). Tamaño, opacidad y lado
// (esquerrans) salen de Configuració.
public class TouchControls : MonoBehaviour
{
    public static TouchControls Instance { get; private set; }

    // ---------- Lo que lee TeamManager ----------

    public Vector2 Move => visible && joystick != null ? joystick.Value : Vector2.zero;
    public bool MoveActive => visible && joystick != null && joystick.Active;
    public bool SprintHeld => visible && sprint != null && sprint.Held;
    public TouchButton Shoot => shoot;
    public TouchButton Pass => pass;
    public TouchButton Action => action;
    public bool Visible => visible;

    // ---------- Medidas (unidades del canvas, altura de referencia 1080) ----------

    const float JoyRadius = 120f;
    const float JoyRestInset = 250f;
    static readonly Vector2 ShootPos = new Vector2(-190f, 190f);
    static readonly Vector2 PassPos = new Vector2(-430f, 120f);
    static readonly Vector2 ActionPos = new Vector2(-150f, 430f);
    static readonly Vector2 SprintPos = new Vector2(-390f, 345f);
    const float ShootSize = 230f, PassSize = 170f, ActionSize = 160f, SprintSize = 140f, PauseSize = 96f;
    const float QteSize = 330f;   // más grande que XUTA: en pleno QTE hay que acertar sin mirar

    static readonly Color ShootColor = new Color(0.95f, 0.47f, 0.17f, 0.85f);
    static readonly Color ButtonColor = new Color(0.16f, 0.21f, 0.28f, 0.8f);
    static readonly Color SprintColor = new Color(0.2f, 0.45f, 0.3f, 0.8f);
    static readonly Color QteColor = new Color(0.25f, 0.8f, 0.38f, 0.95f);

    Canvas canvas;
    CanvasGroup gameGroup, pauseGroup;
    RectTransform joyZone, cluster;
    VirtualJoystick joystick;
    TouchButton shoot, pass, action, sprint, pause, qte;
    Image shootCooldown, actionCooldown;
    PauseController pauseController;
    bool visible, pauseVisible, built, hasBall = true, canShoot = true, qteMode;
    Vector2Int layoutScreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public static TouchControls Ensure()
    {
        if (Instance != null) return Instance;
        Canvas c = UIKit.CreateCanvas("TouchControls", 40);   // debajo del sorteo (90), resultado (150) y pausa (100)
        CanvasScaler scaler = c.GetComponent<CanvasScaler>();
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;                      // tamaño ligado a la altura: igual en 16:9, 4:3 o 20:9
        Instance = c.gameObject.AddComponent<TouchControls>();
        Instance.canvas = c;
        Instance.Build();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplySettings;
        InputMode.Changed += OnSchemeChanged;
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
        InputMode.Changed -= OnSchemeChanged;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnSchemeChanged(ControlScheme s) => ResetAll();

    // ---------- Contexto (lo pone TeamManager cada frame) ----------

    public void SetContext(bool withBall, bool shootAllowed)
    {
        if (!built) return;
        if (withBall != hasBall)
        {
            hasBall = withBall;
            shoot.Label.text = withBall ? "XUTA" : "ROBA";
            action.Label.text = withBall ? "REGAT" : "CANVI";
            ApplyButtons();   // sin bola, PASSA desaparece
        }
        if (shootAllowed != canShoot)
        {
            canShoot = shootAllowed;
            shoot.Label.alpha = shootAllowed ? 1f : 0.45f;
        }
    }

    // 0 = listo, 1 = recién usado. Se pinta como un "reloj" oscuro encima del botón.
    public void SetCooldowns(float shoot01, float action01)
    {
        if (!built) return;
        shootCooldown.fillAmount = Mathf.Clamp01(shoot01);
        actionCooldown.fillAmount = Mathf.Clamp01(action01);
    }

    // ---------- QTE del portero ----------

    // true una vez por pulsación del botón verde (lo llama GoalkeeperQTE)
    public static bool QtePressed() => Instance != null && Instance.built && Instance.qteMode &&
                                       Instance.visible && Instance.qte.ConsumePress();

    // Qué botones se ven: en el QTE solo el verde; si no, todos menos PASSA cuando no llevas la bola.
    // SetActive(false) también suelta el botón (OnDisable -> ResetState): no se queda nada pulsado.
    void ApplyButtons()
    {
        bool normal = !qteMode;
        SetActive(joyZone.gameObject, normal);
        SetActive(shoot.gameObject, normal);
        SetActive(pass.gameObject, normal && hasBall);
        SetActive(action.gameObject, normal);
        SetActive(sprint.gameObject, normal);
        SetActive(qte.gameObject, qteMode);
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go.activeSelf != on) go.SetActive(on);
    }

    // ---------- Visibilidad ----------

    void Update()
    {
        if (!built) return;

        bool touch = InputMode.IsTouch;
        bool hud = !PauseController.IsPaused && !HudVisibility.Hidden;
        GameManager gm = GameManager.Instance;
        bool playing = gm == null || gm.State == MatchState.Kickoff || gm.State == MatchState.Playing;
        bool canPause = gm == null || gm.State != MatchState.FullTime;

        bool inQte = GoalkeeperQTE.IsOpen;
        if (inQte != qteMode)
        {
            qteMode = inQte;
            ApplyButtons();
        }

        SetGameVisible(touch && hud && playing);
        SetPauseVisible(touch && hud && canPause && !qteMode);   // en el QTE, solo el botón verde

        if (pauseVisible && pause.ConsumePress()) PauseGame();

        if (Screen.width != layoutScreen.x || Screen.height != layoutScreen.y) Layout();
    }

    void SetGameVisible(bool v)
    {
        if (v == visible) return;
        visible = v;
        gameGroup.alpha = v ? GameSettings.TouchOpacity : 0f;
        gameGroup.blocksRaycasts = v;
        gameGroup.interactable = v;
        if (!v) ResetGameplay();   // que no se quede un botón "pulsado" al ocultarlo
    }

    void SetPauseVisible(bool v)
    {
        if (v == pauseVisible) return;
        pauseVisible = v;
        pauseGroup.alpha = v ? Mathf.Max(0.6f, GameSettings.TouchOpacity) : 0f;
        pauseGroup.blocksRaycasts = v;
        if (!v) pause.ResetState();
    }

    void PauseGame()
    {
        if (pauseController == null) pauseController = FindFirstObjectByType<PauseController>();
        if (pauseController != null) pauseController.Pause();
        else Debug.LogWarning("[TouchControls] No hay PauseController en la escena: el botón de pausa no hace nada.");
    }

    void ResetGameplay()
    {
        if (joystick != null) joystick.ResetState();
        shoot.ResetState();
        pass.ResetState();
        action.ResetState();
        sprint.ResetState();
        qte.ResetState();
    }

    public void ResetAll()
    {
        if (!built) return;
        ResetGameplay();
        pause.ResetState();
    }

    void OnApplicationFocus(bool focus) { if (!focus) ResetAll(); }
    void OnApplicationPause(bool paused) { if (paused) ResetAll(); }

    // ---------- Construcción ----------

    void Build()
    {
        RectTransform safe = UIKit.Rect(transform, "SafeArea");
        UIKit.Stretch(safe, 0f);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        // Juego (joystick + botones)
        RectTransform game = UIKit.Rect(safe, "Game");
        UIKit.Stretch(game, 0f);
        gameGroup = game.gameObject.AddComponent<CanvasGroup>();

        // Zona del joystick: invisible pero recibe los toques (Image transparente con raycast)
        Image zoneImg = UIKit.Image(game, "JoystickZone", Color.clear, 0f, true);
        joyZone = zoneImg.rectTransform;
        joystick = zoneImg.gameObject.AddComponent<VirtualJoystick>();
        joystick.radius = JoyRadius;

        Image baseImg = UIKit.CircleImage(joyZone, "Base", new Color(1f, 1f, 1f, 0.16f), JoyRadius * 2f + 60f);
        baseImg.raycastTarget = false;
        CenterAnchor(baseImg.rectTransform);
        baseImg.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        Image ring = UIKit.CircleImage(baseImg.transform, "Ring", new Color(1f, 1f, 1f, 0.1f), JoyRadius * 2f);
        CenterAnchor(ring.rectTransform);
        Image knob = UIKit.CircleImage(baseImg.transform, "Knob", new Color(1f, 1f, 1f, 0.75f), 120f);
        CenterAnchor(knob.rectTransform);
        joystick.Base = baseImg.rectTransform;
        joystick.Knob = knob.rectTransform;

        // Botones (en una esquina; el grupo se escala con el ajuste de tamaño)
        cluster = UIKit.Rect(game, "Buttons");
        cluster.sizeDelta = new Vector2(620f, 620f);
        shoot = CreateButton(cluster, "Shoot", "XUTA", ShootSize, ShootColor, 40f, out shootCooldown);
        pass = CreateButton(cluster, "Pass", "PASSA", PassSize, ButtonColor, 30f, out _);
        action = CreateButton(cluster, "Action", "REGAT", ActionSize, ButtonColor, 30f, out actionCooldown);
        sprint = CreateButton(cluster, "Sprint", "SPRINT", SprintSize, SprintColor, 26f, out _);
        qte = CreateButton(cluster, "QTE", "ATURA!", QteSize, QteColor, 56f, out _);
        qte.gameObject.SetActive(false);

        // El tiro se apunta arrastrando desde el botón: un punto enseña hacia dónde
        shoot.dragAim = true;
        Image thumb = UIKit.CircleImage(shoot.transform, "AimThumb", new Color(1f, 1f, 1f, 0.9f), 64f);
        CenterAnchor(thumb.rectTransform);
        thumb.gameObject.SetActive(false);
        shoot.Thumb = thumb.rectTransform;

        // Pausa (aparte: también se ve en el descanso o tras un gol)
        RectTransform pauseRoot = UIKit.Rect(safe, "Pause");
        UIKit.Stretch(pauseRoot, 0f);
        pauseGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
        pause = CreateButton(pauseRoot, "PauseButton", "II", PauseSize, ButtonColor, 34f, out _);
        RectTransform prt = (RectTransform)pause.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
        prt.anchoredPosition = new Vector2(-24f - PauseSize * 0.5f, -24f - PauseSize * 0.5f);

        built = true;
        visible = true;            // para que SetGameVisible(false) aplique el estado inicial
        pauseVisible = true;
        SetGameVisible(false);
        SetPauseVisible(false);
        ApplySettings();
    }

    TouchButton CreateButton(RectTransform parent, string name, string label, float size, Color color,
                             float fontSize, out Image cooldown)
    {
        Image face = UIKit.CircleImage(parent, name, color, size);
        face.raycastTarget = true;
        face.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        // Borde claro para que se distinga sobre la pista
        Image rim = UIKit.CircleImage(face.transform, "Rim", new Color(1f, 1f, 1f, 0.22f), size);
        CenterAnchor(rim.rectTransform);
        rim.transform.SetAsFirstSibling();
        Image inner = UIKit.CircleImage(face.transform, "Inner", color, size - 8f);
        CenterAnchor(inner.rectTransform);

        cooldown = UIKit.CircleImage(face.transform, "Cooldown", new Color(0f, 0f, 0f, 0.5f), size - 8f);
        CenterAnchor(cooldown.rectTransform);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Radial360;
        cooldown.fillOrigin = (int)Image.Origin360.Top;
        cooldown.fillClockwise = false;
        cooldown.fillAmount = 0f;

        TextMeshProUGUI t = UIKit.Text(face.transform, "Label", label, fontSize, Color.white, FontStyles.Bold);
        t.characterSpacing = 2f;
        t.enableAutoSizing = true;
        t.fontSizeMin = 14f;
        t.fontSizeMax = fontSize;
        UIKit.Stretch(t.rectTransform, size * 0.12f);

        // La cara (con raycast) es transparente: el color lo lleva "Inner", así el borde se ve
        TouchButton b = face.gameObject.AddComponent<TouchButton>();
        b.Setup(inner, t, color);
        face.color = new Color(1f, 1f, 1f, 0.001f);   // casi invisible pero sigue recibiendo toques
        return b;
    }

    static void CenterAnchor(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    // ---------- Ajustes (tamaño, opacidad, esquerrans) ----------

    void ApplySettings()
    {
        if (!built) return;
        if (visible) gameGroup.alpha = GameSettings.TouchOpacity;
        if (pauseVisible) pauseGroup.alpha = Mathf.Max(0.6f, GameSettings.TouchOpacity);
        Layout();
    }

    void Layout()
    {
        if (!built) return;
        layoutScreen = new Vector2Int(Screen.width, Screen.height);

        bool left = GameSettings.TouchLeftHanded;
        float s = GameSettings.TouchScale;

        // Joystick: media pantalla del lado contrario a los botones, sin llegar arriba (marcador / pausa)
        joyZone.anchorMin = new Vector2(left ? 0.55f : 0f, 0f);
        joyZone.anchorMax = new Vector2(left ? 1f : 0.45f, 0.75f);
        joyZone.pivot = new Vector2(0.5f, 0.5f);
        joyZone.offsetMin = joyZone.offsetMax = Vector2.zero;

        joystick.radius = JoyRadius * s;
        joystick.Base.sizeDelta = Vector2.one * (JoyRadius * 2f + 60f) * s;
        joystick.Base.Find("Ring").GetComponent<RectTransform>().sizeDelta = Vector2.one * JoyRadius * 2f * s;
        joystick.Knob.sizeDelta = Vector2.one * 120f * s;

        Canvas.ForceUpdateCanvases();   // para que joyZone.rect ya tenga el tamaño nuevo
        Rect r = joyZone.rect;
        float inset = JoyRestInset * s;
        joystick.RestPosition = new Vector2(left ? r.xMax - inset : r.xMin + inset, r.yMin + inset);
        joystick.ResetState();

        // Botones en la esquina inferior (derecha, o izquierda para esquerrans)
        Vector2 corner = left ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
        cluster.anchorMin = cluster.anchorMax = cluster.pivot = corner;
        cluster.anchoredPosition = Vector2.zero;
        cluster.localScale = new Vector3(s, s, 1f);
        PlaceButton(shoot, ShootPos, corner, left);
        PlaceButton(pass, PassPos, corner, left);
        PlaceButton(action, ActionPos, corner, left);
        PlaceButton(sprint, SprintPos, corner, left);
        PlaceButton(qte, ShootPos, corner, left);   // en el sitio del botón principal
    }

    static void PlaceButton(TouchButton b, Vector2 rightHandedPos, Vector2 corner, bool mirror)
    {
        RectTransform rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = corner;
        rt.anchoredPosition = mirror ? new Vector2(-rightHandedPos.x, rightHandedPos.y) : rightHandedPos;
    }
}
