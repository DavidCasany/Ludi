using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Esquema de control activo: teclado + ratón (o mando) o pantalla táctil.
//
// Con el ajuste "Automàtic" (por defecto):
//  - al arrancar se mira si el dispositivo es táctil de verdad (móvil/tableta: puntero "grueso");
//  - tocar la pantalla pasa a Táctil al momento;
//  - pulsar una tecla, un botón del mando o mover el ratón vuelve a Teclado/ratón. Los navegadores
//    "inventan" eventos de ratón después de cada toque, por eso el ratón solo cuenta si hace un rato
//    que no se toca la pantalla.
// Con "Teclat i ratolí" o "Tàctil" (Configuració) se fuerza ese esquema y no se cambia solo.
//
// Lo consultan TeamManager (qué input leer), TouchControls (si se ven los botones), GoalkeeperQTE,
// los textos de ayuda (InputHint)... Si cambia, se lanza Changed.
public enum ControlScheme { KeyboardMouse, Touch }

public static class InputMode
{
    public static ControlScheme Current { get; private set; } = ControlScheme.KeyboardMouse;
    public static bool IsTouch => Current == ControlScheme.Touch;

    public static event Action<ControlScheme> Changed;

    // Tras tocar la pantalla, el "ratón" que mueve el navegador no cuenta durante este tiempo
    const float MouseIgnoreAfterTouch = 1.2f;
    const float MouseMoveThreshold = 3f;   // píxeles por frame

    static Runner runner;
    static ControlPreference lastPreference;
    static float lastTouchTime = -100f;
    static Vector3 lastMousePos;
    static bool mouseInit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
        runner = null;
        lastTouchTime = -100f;
        mouseInit = false;
        Current = ControlScheme.KeyboardMouse;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        GameObject go = new GameObject("[InputMode]") { hideFlags = HideFlags.HideInHierarchy };
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<Runner>();

        GameSettings.EnsureLoaded();   // el orden de los RuntimeInitializeOnLoadMethod no está garantizado
        lastPreference = GameSettings.Controls;
        Detect(true);
        GameSettings.Changed += ApplyPreference;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // GameSettings.Changed salta con cualquier ajuste (volumen...): solo se actúa si cambia el de controles
    static void ApplyPreference()
    {
        if (GameSettings.Controls == lastPreference) return;
        lastPreference = GameSettings.Controls;
        Detect(true);
    }

    // Auto respeta la detección; los otros fuerzan el esquema
    static void Detect(bool fromDevice)
    {
        switch (GameSettings.Controls)
        {
            case ControlPreference.Touch: Set(ControlScheme.Touch); break;
            case ControlPreference.KeyboardMouse: Set(ControlScheme.KeyboardMouse); break;
            default:
                // Al arrancar o al volver a "Automàtic": si se acaba de tocar la pantalla se queda en táctil
                if (fromDevice && Time.realtimeSinceStartup - lastTouchTime > MouseIgnoreAfterTouch)
                    Set(WebPlatform.IsTouchPrimary() ? ControlScheme.Touch : ControlScheme.KeyboardMouse);
                break;
        }
    }

    static void Set(ControlScheme scheme)
    {
        if (Current == scheme) return;
        Current = scheme;
        Changed?.Invoke(scheme);
    }

    static bool Auto => GameSettings.Controls == ControlPreference.Auto;

    // ---------- Detección (cada frame, antes que cualquier otro script) ----------

    static void DetectFrame()
    {
        TouchInput.Sample();

        if (TouchInput.ActiveCount > 0 || TouchInput.AnyBegan)
        {
            lastTouchTime = Time.realtimeSinceStartup;
            if (Auto) Set(ControlScheme.Touch);
            mouseInit = false;
            return;
        }

        bool keyOrPad = KeyboardOrGamepadPressed();
        bool mouse = RealMouseUsed();
        if (Auto && (keyOrPad || mouse)) Set(ControlScheme.KeyboardMouse);
    }

    static bool KeyboardOrGamepadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.anyKey.wasPressedThisFrame) return true;
        var g = UnityEngine.InputSystem.Gamepad.current;
        if (g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame ||
                          g.startButton.wasPressedThisFrame || g.leftStick.ReadValue().sqrMagnitude > 0.25f))
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        // anyKeyDown también cuenta los botones del ratón, que el navegador simula con los toques
        if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) &&
            !Input.GetMouseButtonDown(2))
            return true;
#endif
        return false;
    }

    static bool RealMouseUsed()
    {
        if (Time.realtimeSinceStartup - lastTouchTime < MouseIgnoreAfterTouch) return false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!Input.mousePresent) return false;
        Vector3 p = Input.mousePosition;
        bool moved = mouseInit && (p - lastMousePos).sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold;
        lastMousePos = p;
        mouseInit = true;
        return moved || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#elif ENABLE_INPUT_SYSTEM
        var m = UnityEngine.InputSystem.Mouse.current;
        if (m == null) return false;
        return m.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold ||
               m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame;
#else
        return false;
#endif
    }

    // ---------- Textos de ayuda de las escenas ----------

    // Las pantallas hechas con los constructores del editor llevan un texto "Esc · Tornar" (objeto "Hint").
    // Con el dedo no hay Esc: se ocultan solos en modo táctil.
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Hint" && t.text.Contains("Esc")) InputHint.Attach(t, null);
    }

    [DefaultExecutionOrder(-10000)]
    class Runner : MonoBehaviour
    {
        void Update() => DetectFrame();

        // Al volver a la pestaña o a la app, los dedos que había en la pantalla ya no existen
        void OnApplicationFocus(bool focus) { if (!focus) TouchInput.Clear(); }
        void OnApplicationPause(bool paused) { if (paused) TouchInput.Clear(); }
    }
}

// Toques "crudos" de este frame, con el Input Manager clásico o con el Input System (el que esté activo).
// Para los botones en pantalla NO se usa esto: van por el EventSystem (TouchButton, VirtualJoystick).
public static class TouchInput
{
    public static int ActiveCount { get; private set; }
    public static bool AnyBegan => began.Count > 0;

    static readonly List<Vector2> began = new List<Vector2>();
    static readonly List<Vector2> ended = new List<Vector2>();
    static int sampledFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Clear(); }

    public static void Clear()
    {
        began.Clear();
        ended.Clear();
        ActiveCount = 0;
        sampledFrame = -1;
    }

    // Posiciones (píxeles de pantalla, origen abajo a la izquierda) de los toques que empiezan / acaban este frame
    public static IReadOnlyList<Vector2> Began { get { Sample(); return began; } }
    public static IReadOnlyList<Vector2> Ended { get { Sample(); return ended; } }

    // Lo llama InputMode al principio de cada frame; también es seguro llamarlo varias veces
    public static void Sample()
    {
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        began.Clear();
        ended.Clear();
        ActiveCount = 0;

#if ENABLE_LEGACY_INPUT_MANAGER
        int n = Input.touchCount;
        for (int i = 0; i < n; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began) began.Add(t.position);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) ended.Add(t.position);
            else ActiveCount++;
        }
        if (n > 0) return;
#endif
#if ENABLE_INPUT_SYSTEM
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts == null) return;
        foreach (var t in ts.touches)
        {
            if (t.press.wasPressedThisFrame) began.Add(t.position.ReadValue());
            if (t.press.wasReleasedThisFrame) ended.Add(t.position.ReadValue());
            if (t.press.isPressed) ActiveCount++;
        }
#endif
    }

    // ¿Algún dedo se ha levantado este frame dentro de este rectángulo (píxeles de pantalla)?
    public static bool TapEndedIn(Rect screenRect)
    {
        foreach (Vector2 p in Ended)
            if (screenRect.Contains(p)) return true;
        return false;
    }

    // ¿Este evento de la UI viene de un dedo? (para los componentes que reaccionan al "hover")
    public static bool IsTouchPointer(PointerEventData e)
    {
        if (e == null) return InputMode.IsTouch;
#if ENABLE_INPUT_SYSTEM
        if (e is UnityEngine.InputSystem.UI.ExtendedPointerEventData x)
            return x.pointerType == UnityEngine.InputSystem.UI.UIPointerType.Touch;
#endif
        // StandaloneInputModule: el ratón usa ids negativos (-1, -2, -3) y cada dedo su fingerId (>= 0)
        return e.pointerId >= 0;
    }
}
