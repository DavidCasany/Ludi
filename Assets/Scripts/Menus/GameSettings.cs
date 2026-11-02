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

    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
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
        ApplyDisplay();
        Save();
    }

    // ---------- Resoluciones ----------

    // Resoluciones del SettingsData que caben en el monitor, más la actual (por si no estuviera en la lista)
    public static List<Vector2Int> AvailableResolutions()
    {
        int maxW = Display.main.systemWidth, maxH = Display.main.systemHeight;
        List<Vector2Int> list = new List<Vector2Int>();

        foreach (Vector2Int r in AllResolutions())
            if (r.x <= maxW && r.y <= maxH && !list.Contains(r)) list.Add(r);

        Vector2Int current = new Vector2Int(Width, Height);
        if (!list.Contains(current)) list.Add(current);

        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
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
        Vector2Int res = d != null ? d.DefaultResolution : new Vector2Int(1920, 1080);
        Fullscreen = d == null || d.DefaultFullscreen;
        MusicVolume = d != null ? d.DefaultMusicVolume : 0.8f;
        SfxVolume = d != null ? d.DefaultSfxVolume : 0.8f;
        PassArrows = d == null || d.DefaultPassArrows;

        // Si el monitor es más pequeño que la resolución por defecto (p. ej. un portátil 1366×768),
        // se usa la mayor de la lista que quepa, para que la ventana no se salga de la pantalla
        int maxW = Display.main.systemWidth, maxH = Display.main.systemHeight;
        if (res.x > maxW || res.y > maxH)
        {
            Vector2Int best = new Vector2Int(maxW, maxH);
            int bestArea = 0;
            foreach (Vector2Int r in AllResolutions())
                if (r.x <= maxW && r.y <= maxH && r.x * r.y > bestArea) { best = r; bestArea = r.x * r.y; }
            res = best;
        }

        Width = res.x;
        Height = res.y;
    }

    static void Save()
    {
        PlayerPrefs.SetInt(KeyWidth, Width);
        PlayerPrefs.SetInt(KeyHeight, Height);
        PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetFloat(KeyMusic, MusicVolume);
        PlayerPrefs.SetFloat(KeySfx, SfxVolume);
        PlayerPrefs.SetInt(KeyArrows, PassArrows ? 1 : 0);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
