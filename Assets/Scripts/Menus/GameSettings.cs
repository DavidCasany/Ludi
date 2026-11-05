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
    const string KeyMonitor = "settings_monitor";
    const string KeyMonitorName = "settings_monitor_name";
    const string KeyVersion = "settings_version";

    // Súbelo cuando cambien los valores por defecto o el formato: los ajustes guardados con otra versión
    // (p. ej. los de las pruebas en el editor) se descartan y se vuelve a los de SettingsData.
    const int Version = 4;   // 4: música al 5 % i efectes al 75 % per defecte (descarta guardats amb el volum a 0)

    // Las claves nuevas (controles táctiles, render web) no obligan a subir Version: si faltan en un guardado
    // antiguo se usa el valor por defecto
    static readonly string[] AllKeys = { KeyWidth, KeyHeight, KeyFullscreen, KeyMusic, KeySfx, KeyArrows,
                                         KeyControls, KeyTouchScale, KeyTouchOpacity, KeyTouchLeft, KeyRenderScale,
                                         KeyMonitor, KeyMonitorName, KeyVersion };

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

    // Monitor on es mostra el joc (índex dins de Monitors()). -1 = el que triï el sistema
    public static int Monitor { get; private set; } = -1;
    static string monitorName = "";

    // Se lanza tras cualquier cambio (lo escuchan los AudioVolume, por ejemplo)
    public static event Action Changed;

    // Quan la finestra acaba de canviar de monitor (la llista de resolucions pot ser una altra)
    public static event Action DisplayChanged;

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
        DisplayChanged = null;
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
        RestoreMonitor();
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

    // ---------- Monitors (ordinador amb més d'una pantalla) ----------

    // Pantalles connectades, en l'ordre del sistema. A la web i al mòbil no n'hi ha cap per triar.
    public static List<DisplayInfo> Monitors()
    {
        List<DisplayInfo> list = new List<DisplayInfo>();
        if (!IsWeb && !IsNativeMobile) Screen.GetDisplayLayout(list);
        return list;
    }

    public static bool CanChooseMonitor => Monitors().Count > 1;

    // Índex del monitor on és ara la finestra del joc
    public static int CurrentMonitor()
    {
        List<DisplayInfo> list = Monitors();
        DisplayInfo now = Screen.mainWindowDisplayInfo;
        for (int i = 0; i < list.Count; i++)
            if (SameMonitor(list[i], now)) return i;
        return 0;
    }

    static bool SameMonitor(DisplayInfo a, DisplayInfo b) =>
        a.name == b.name && a.width == b.width && a.height == b.height && a.workArea.x == b.workArea.x &&
        a.workArea.y == b.workArea.y;

    // Mou la finestra al monitor 'index'. En pantalla completa passa a la resolució nativa d'aquell monitor;
    // en finestra es queda la mateixa mida (si hi cap) i es centra. A l'editor no fa res (no hi ha finestra).
    public static void SetMonitor(int index)
    {
        List<DisplayInfo> list = Monitors();
        if (index < 0 || index >= list.Count) return;

        Monitor = index;
        monitorName = list[index].name;
        Save();
        MoveTo(list[index]);
    }

    static void RestoreMonitor()
    {
        if (Monitor < 0) return;
        List<DisplayInfo> list = Monitors();
        if (list.Count < 2) return;

        // Primer pel nom (l'ordre pot canviar si es connecten pantalles); si no, per l'índex
        int index = list.FindIndex(d => d.name == monitorName);
        if (index < 0) index = Monitor < list.Count ? Monitor : -1;
        if (index < 0 || SameMonitor(list[index], Screen.mainWindowDisplayInfo)) return;
        MoveTo(list[index]);
    }

    static void MoveTo(DisplayInfo d)
    {
#if UNITY_EDITOR
        Debug.Log($"[GameSettings] Monitor: «{d.name}». A l'editor no es mou la finestra: prova-ho al joc compilat.");
        DisplayChanged?.Invoke();   // (no canvia res, però la pantalla de Configuració es refresca igual)
#else
        Vector2Int pos = Vector2Int.zero;
        if (!Fullscreen)
        {
            int w = Mathf.Min(Width, d.workArea.width), h = Mathf.Min(Height, d.workArea.height);
            pos = new Vector2Int(Mathf.Max(0, (d.workArea.width - w) / 2), Mathf.Max(0, (d.workArea.height - h) / 2));
        }
        AsyncOperation op = Screen.MoveMainWindowTo(d, pos);
        op.completed += _ =>
        {
            if (Fullscreen)
            {
                Width = d.width;
                Height = d.height;
            }
            ClampToAvailable();
            ApplyDisplay();
            Save();
            DisplayChanged?.Invoke();
        };
#endif
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
        // Web i mòbil: no hi ha "finestra en un monitor". Screen.mainWindowDisplayInfo hi llança una excepció
        // i, com que LoadDefaults el crida primer, deixava tots els ajustos a 0 / false.
        if (IsWeb || IsNativeMobile) return new Vector2Int(Screen.width, Screen.height);

        // La del monitor on és la finestra (amb dues pantalles poden ser diferents)
        try
        {
            DisplayInfo d = Screen.mainWindowDisplayInfo;
            if (d.width > 0 && d.height > 0) return new Vector2Int(d.width, d.height);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameSettings] No s'ha pogut llegir el monitor: " + e.Message);
        }
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
        Monitor = PlayerPrefs.GetInt(KeyMonitor, -1);
        monitorName = PlayerPrefs.GetString(KeyMonitorName, "");
    }

    static void LoadDefaults()
    {
        SettingsData d = Data;
        Fullscreen = d == null || d.DefaultFullscreen;
        MusicVolume = d != null ? d.DefaultMusicVolume : 0.05f;
        SfxVolume = d != null ? d.DefaultSfxVolume : 0.75f;
        Monitor = -1;
        monitorName = "";
        PassArrows = d == null || d.DefaultPassArrows;
        Controls = d != null ? d.DefaultControls : ControlPreference.Auto;
        TouchScale = d != null ? Mathf.Clamp(d.DefaultTouchScale, TouchScaleMin, TouchScaleMax) : 1f;
        TouchOpacity = d != null ? Mathf.Clamp(d.DefaultTouchOpacity, TouchOpacityMin, TouchOpacityMax) : 0.7f;
        TouchLeftHanded = d != null && d.DefaultTouchLeftHanded;
        RenderScale = d != null ? Mathf.Clamp(d.DefaultRenderScale, 0f, 1f) : 0f;

        // La resolució va l'última: si fallés, la resta d'ajustos ja tindrien el seu valor per defecte.
        // Si no cabe en el monitor (p. ej. un portátil 1366×768), ClampToAvailable la baja después
        Vector2Int res = d == null || d.DefaultToNative ? NativeResolution() : d.DefaultResolution;
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
        PlayerPrefs.SetInt(KeyMonitor, Monitor);
        PlayerPrefs.SetString(KeyMonitorName, monitorName);
        PlayerPrefs.SetInt(KeyVersion, Version);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
