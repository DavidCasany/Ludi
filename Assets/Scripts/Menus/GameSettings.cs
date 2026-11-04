using System;
using System.Collections.Generic;
using UnityEngine;

// Automàtic = según el dispositivo y lo último que se ha usado (InputMode); los otros lo fuerzan
public enum ControlPreference { Auto = 0, KeyboardMouse = 1, Touch = 2 }

// Ajustes actuales del juego. Estático: cualquier escena los lee sin referencias
// (GameSettings.MusicVolume, GameSettings.PassArrows...).
//  - Se cargan de PlayerPrefs al arrancar el juego, antes de la primera escena, y se aplican.
//  - Cada Set... aplica el cambio al momento, lo guarda y lanza Changed.
//  - Si nunca se ha guardado nada, se usan los valores por defecto de SettingsData.
//  - Web / móvil: la resolución la pone la página (en su lugar hay "resolución de render") y se añaden
//    los ajustes del modo de control y de los controles táctiles.
public static class GameSettings
{
    const string KeyWidth = "settings_width";
    const string KeyHeight = "settings_height";
    const string KeyFullscreen = "settings_fullscreen";
    const string KeyMusic = "settings_music";
    const string KeySfx = "settings_sfx";
    const string KeyArrows = "settings_pass_arrows";
    const string KeyControls = "settings_controls";
    const string KeyTouchScale = "settings_touch_scale";
    const string KeyTouchOpacity = "settings_touch_opacity";
    const string KeyTouchLeft = "settings_touch_left";
    const string KeyRenderScale = "settings_render_scale";
    const string KeyVersion = "settings_version";

    // Súbelo cuando cambien los valores por defecto o el formato: los ajustes guardados con otra versión
    // (p. ej. los de las pruebas en el editor) se descartan y se vuelve a los de SettingsData.
    const int Version = 2;

    // Las claves nuevas (controles táctiles, render web) no obligan a subir Version: si faltan en un guardado
    // antiguo se usa el valor por defecto
    static readonly string[] AllKeys = { KeyWidth, KeyHeight, KeyFullscreen, KeyMusic, KeySfx, KeyArrows,
                                         KeyControls, KeyTouchScale, KeyTouchOpacity, KeyTouchLeft, KeyRenderScale,
                                         KeyVersion };

    // Límites de los controles táctiles (los usan también los sliders de Configuració)
    public const float TouchScaleMin = 0.7f, TouchScaleMax = 1.4f;
    public const float TouchOpacityMin = 0.25f, TouchOpacityMax = 1f;

    // Escalas de render que ofrece el desplegable en la web (0 = automática)
    public static readonly float[] RenderScales = { 0f, 1f, 0.75f, 0.5f };

    public static int Width { get; private set; }
    public static int Height { get; private set; }
    public static bool Fullscreen { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }
    public static bool PassArrows { get; private set; }
    public static ControlPreference Controls { get; private set; }
    public static float TouchScale { get; private set; } = 1f;
    public static float TouchOpacity { get; private set; } = 0.7f;
    public static bool TouchLeftHanded { get; private set; }
    // Solo web: resolución interna del canvas respecto a la de la pantalla (0 = automática)
    public static float RenderScale { get; private set; }

    // Se lanza tras cualquier cambio (lo escuchan los AudioVolume, por ejemplo)
    public static event Action Changed;

    static SettingsData data;
    static bool loaded;

    public static SettingsData Data
    {
        get
        {
            if (data == null) data = Resources.Load<SettingsData>("SettingsData");
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
        data = null;
        loaded = false;
    }

    // En el navegador (WebGL) la resolución la decide la ventana/página y la pantalla completa solo se puede
    // pedir desde un clic del jugador: no se toca nada al arrancar y el desplegable de resolución se oculta.
    public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

    // Build nativa de Android/iOS: la resolución y la pantalla completa las decide el sistema
    public static bool IsNativeMobile => Application.isMobilePlatform && !IsWeb;

    // Por si alguien lee los ajustes antes que Init (el orden de los RuntimeInitializeOnLoadMethod no es fijo)
    public static void EnsureLoaded() => Load();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Load();
        if (IsWeb)
        {
            Fullscreen = false;   // el navegador siempre arranca en ventana
            WebPlatform.SetRenderScale(RenderScale);
            return;
        }
        if (IsNativeMobile) return;   // siempre pantalla completa a la resolución del dispositivo
        ClampToAvailable();   // p. ej. se guardó 2560×1440 y ahora el monitor es de 1920×1080
        ApplyDisplay();
    }

    // ---------- Cambios ----------

    public static void SetResolution(int width, int height)
    {
        if (IsWeb || IsNativeMobile) return;
        Width = width;
        Height = height;
        ApplyDisplay();
        Save();
    }

    // Al pasar a ventana con la resolución nativa, la ventana no cabría (bordes + barra de tareas):
    // se baja a la mayor que sí cabe. El menú de ajustes vuelve a leer la resolución después.
    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
        if (IsWeb)
        {
            Screen.fullScreen = on;   // Unity lo pide al navegador en el mismo clic del jugador
            Save();
            return;
        }
        if (IsNativeMobile) { Save(); return; }
        ClampToAvailable();
        ApplyDisplay();
        Save();
    }

    public static void SetMusicVolume(float v)
    {
        MusicVolume = Mathf.Clamp01(v);
        Save();
    }

    public static void SetSfxVolume(float v)
    {
        SfxVolume = Mathf.Clamp01(v);
        Save();
    }

    public static void SetPassArrows(bool on)
    {
        PassArrows = on;
        Save();
    }

    public static void SetControls(ControlPreference p)
    {
        Controls = p;
        Save();
    }

    public static void SetTouchScale(float v)
    {
        TouchScale = Mathf.Clamp(v, TouchScaleMin, TouchScaleMax);
        Save();
    }

    public static void SetTouchOpacity(float v)
    {
        TouchOpacity = Mathf.Clamp(v, TouchOpacityMin, TouchOpacityMax);
        Save();
    }

    public static void SetTouchLeftHanded(bool on)
    {
        TouchLeftHanded = on;
        Save();
    }

    // Solo hace algo en la web: cambia la resolución interna del canvas al momento
    public static void SetRenderScale(float scale)
    {
        RenderScale = Mathf.Clamp(scale, 0f, 1f);
        if (IsWeb) WebPlatform.SetRenderScale(RenderScale);
        Save();
    }

    public static void ResetToDefaults()
    {
        LoadDefaults();
        if (IsWeb) WebPlatform.SetRenderScale(RenderScale);
        else if (!IsNativeMobile)
        {
            ClampToAvailable();
            ApplyDisplay();
        }
        Save();
    }

    // ---------- Resoluciones ----------

    // Resolución real del monitor. En el editor Display.main devuelve el tamaño de la Game view,
    // por eso allí se usa la del escritorio.
    public static Vector2Int NativeResolution()
    {
#if UNITY_EDITOR
        Resolution r = Screen.currentResolution;
        return new Vector2Int(r.width, r.height);
#else
        return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight);
#endif
    }

    // Lo que ofrece el desplegable con el modo actual (pantalla completa o ventana):
    // las que soporta el monitor + las del SettingsData + la nativa, sin repetir y sin las que no caben.
    public static List<Vector2Int> AvailableResolutions() => AvailableResolutions(Fullscreen);

    static List<Vector2Int> AvailableResolutions(bool fullscreen)
    {
        Vector2Int native = NativeResolution();
        List<Vector2Int> list = new List<Vector2Int>();

        foreach (Vector2Int r in AllResolutions()) TryAdd(list, r, native, fullscreen);
        foreach (Resolution r in Screen.resolutions)   // un modo por frecuencia: se quedan sin repetir
            TryAdd(list, new Vector2Int(r.width, r.height), native, fullscreen);
        TryAdd(list, native, native, fullscreen);

        if (list.Count == 0) list.Add(fullscreen ? native : new Vector2Int(1280, 720));
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    static void TryAdd(List<Vector2Int> list, Vector2Int r, Vector2Int native, bool fullscreen)
    {
        if (r.x < 1024 || r.y < 600) return;   // modos antiguos que algunos monitores siguen anunciando
        // En pantalla completa vale hasta la nativa; en ventana tiene que ser más pequeña (bordes y barra de tareas)
        bool fits = fullscreen ? r.x <= native.x && r.y <= native.y : r.x < native.x && r.y < native.y;
        if (fits && !list.Contains(r)) list.Add(r);
    }

    // Si la resolución actual no está en la lista del modo actual, se usa la mayor que no la supere
    // (o la más pequeña de la lista si todas la superan)
    static void ClampToAvailable()
    {
        List<Vector2Int> list = AvailableResolutions(Fullscreen);
        Vector2Int current = new Vector2Int(Width, Height);
        if (list.Contains(current)) return;

        Vector2Int best = list[0];
        foreach (Vector2Int r in list)
            if (r.x <= current.x && r.y <= current.y && r.x * r.y > best.x * best.y) best = r;
        Width = best.x;
        Height = best.y;
    }

    static IEnumerable<Vector2Int> AllResolutions()
    {
        if (Data != null && Data.Resolutions.Count > 0) return Data.Resolutions;
        return new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };
    }

    // ---------- Aplicar ----------

    // En el editor Screen.SetResolution no hace nada: la resolución solo cambia en el juego compilado
    static void ApplyDisplay()
    {
        Screen.SetResolution(Width, Height, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    // ---------- Guardado ----------

    static void Load()
    {
        if (loaded) return;
        loaded = true;

        LoadDefaults();
        if (!PlayerPrefs.HasKey(KeyWidth)) return;   // primera vez: valores por defecto
        if (PlayerPrefs.GetInt(KeyVersion, 0) != Version)
        {
            DeleteSaved();                           // guardados con otra versión: fuera
            return;
        }

        Width = PlayerPrefs.GetInt(KeyWidth, Width);
        Height = PlayerPrefs.GetInt(KeyHeight, Height);
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Fullscreen ? 1 : 0) == 1;
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, MusicVolume);
        SfxVolume = PlayerPrefs.GetFloat(KeySfx, SfxVolume);
        PassArrows = PlayerPrefs.GetInt(KeyArrows, PassArrows ? 1 : 0) == 1;
        Controls = (ControlPreference)Mathf.Clamp(PlayerPrefs.GetInt(KeyControls, (int)Controls), 0, 2);
        TouchScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyTouchScale, TouchScale), TouchScaleMin, TouchScaleMax);
        TouchOpacity = Mathf.Clamp(PlayerPrefs.GetFloat(KeyTouchOpacity, TouchOpacity), TouchOpacityMin, TouchOpacityMax);
        TouchLeftHanded = PlayerPrefs.GetInt(KeyTouchLeft, TouchLeftHanded ? 1 : 0) == 1;
        RenderScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyRenderScale, RenderScale), 0f, 1f);
    }

    static void LoadDefaults()
    {
        SettingsData d = Data;
        Vector2Int res = d == null || d.DefaultToNative ? NativeResolution() : d.DefaultResolution;
        Fullscreen = d == null || d.DefaultFullscreen;
        MusicVolume = d != null ? d.DefaultMusicVolume : 0.05f;
        SfxVolume = d != null ? d.DefaultSfxVolume : 0.8f;
        PassArrows = d == null || d.DefaultPassArrows;
        Controls = d != null ? d.DefaultControls : ControlPreference.Auto;
        TouchScale = d != null ? Mathf.Clamp(d.DefaultTouchScale, TouchScaleMin, TouchScaleMax) : 1f;
        TouchOpacity = d != null ? Mathf.Clamp(d.DefaultTouchOpacity, TouchOpacityMin, TouchOpacityMax) : 0.7f;
        TouchLeftHanded = d != null && d.DefaultTouchLeftHanded;
        RenderScale = d != null ? Mathf.Clamp(d.DefaultRenderScale, 0f, 1f) : 0f;

        // Si no cabe en el monitor (p. ej. un portátil 1366×768), ClampToAvailable la baja después
        Width = res.x;
        Height = res.y;
    }

    // Borra solo las claves de ajustes (no toca otros PlayerPrefs, como la partida de torneo)
    public static void DeleteSaved()
    {
        foreach (string k in AllKeys) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
    }

    static void Save()
    {
        PlayerPrefs.SetInt(KeyWidth, Width);
        PlayerPrefs.SetInt(KeyHeight, Height);
        PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        PlayerPrefs.SetInt(KeyArrows, PassArrows ? 1 : 0);
        PlayerPrefs.SetInt(KeyControls, (int)Controls);
        PlayerPrefs.SetFloat(KeyTouchScale, TouchScale);
        PlayerPrefs.SetFloat(KeyTouchOpacity, TouchOpacity);
        PlayerPrefs.SetInt(KeyTouchLeft, TouchLeftHanded ? 1 : 0);
        PlayerPrefs.SetFloat(KeyRenderScale, RenderScale);
        PlayerPrefs.SetInt(KeyVersion, Version);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
