using System;
using System.Collections.Generic;
using UnityEngine;

// Ajustes actuales del juego. Estático: cualquier escena los lee sin referencias
// (GameSettings.MusicVolume, GameSettings.PassArrows...).
//  - Se cargan de PlayerPrefs al arrancar el juego, antes de la primera escena, y se aplican.
//  - Cada Set... aplica el cambio al momento, lo guarda y lanza Changed.
//  - Si nunca se ha guardado nada, se usan los valores por defecto de SettingsData.
public static class GameSettings
{
    const string KeyWidth = "settings_width";
    const string KeyHeight = "settings_height";
    const string KeyFullscreen = "settings_fullscreen";
    const string KeyMusic = "settings_music";
    const string KeySfx = "settings_sfx";
    const string KeyArrows = "settings_pass_arrows";
    const string KeyVersion = "settings_version";

    // Súbelo cuando cambien los valores por defecto o el formato: los ajustes guardados con otra versión
    // (p. ej. los de las pruebas en el editor) se descartan y se vuelve a los de SettingsData.
    const int Version = 2;

    static readonly string[] AllKeys = { KeyWidth, KeyHeight, KeyFullscreen, KeyMusic, KeySfx, KeyArrows, KeyVersion };

    public static int Width { get; private set; }
    public static int Height { get; private set; }
    public static bool Fullscreen { get; private set; }
    public static float MusicVolume { get; private set; }
    public static float SfxVolume { get; private set; }
    public static bool PassArrows { get; private set; }

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Load();
        ClampToAvailable();   // p. ej. se guardó 2560×1440 y ahora el monitor es de 1920×1080
        ApplyDisplay();
    }

    // ---------- Cambios ----------

    public static void SetResolution(int width, int height)
    {
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

    public static void ResetToDefaults()
    {
        LoadDefaults();
        ClampToAvailable();
        ApplyDisplay();
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
    }

    static void LoadDefaults()
    {
        SettingsData d = Data;
        Vector2Int res = d == null || d.DefaultToNative ? NativeResolution() : d.DefaultResolution;
        Fullscreen = d == null || d.DefaultFullscreen;
        MusicVolume = d != null ? d.DefaultMusicVolume : 0.8f;
        SfxVolume = d != null ? d.DefaultSfxVolume : 0.8f;
        PassArrows = d == null || d.DefaultPassArrows;

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
        PlayerPrefs.SetInt(KeyVersion, Version);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
