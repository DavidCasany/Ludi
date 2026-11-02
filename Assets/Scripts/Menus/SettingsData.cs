using System.Collections.Generic;
using UnityEngine;

// Valores por defecto de los ajustes y lista de resoluciones que se ofrecen.
// El asset debe llamarse "SettingsData" y estar en una carpeta Resources (Assets/Resources/SettingsData.asset)
// para que GameSettings lo encuentre sin referencias. El constructor del menú de ajustes lo crea solo.
[CreateAssetMenu(menuName = "Ludi/Settings Data", fileName = "SettingsData")]
public class SettingsData : ScriptableObject
{
    [Header("Valores por defecto")]
    [SerializeField] Vector2Int defaultResolution = new Vector2Int(1920, 1080);
    [SerializeField] bool defaultFullscreen = true;
    [SerializeField, Range(0f, 1f)] float defaultMusicVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] float defaultSfxVolume = 0.8f;
    [SerializeField] bool defaultPassArrows = true;

    [Header("Resoluciones del desplegable (las mayores que el monitor se ocultan)")]
    [SerializeField] List<Vector2Int> resolutions = new List<Vector2Int>
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160)
    };

    public Vector2Int DefaultResolution => defaultResolution;
    public bool DefaultFullscreen => defaultFullscreen;
    public float DefaultMusicVolume => defaultMusicVolume;
    public float DefaultSfxVolume => defaultSfxVolume;
    public bool DefaultPassArrows => defaultPassArrows;
    public IReadOnlyList<Vector2Int> Resolutions => resolutions;
}
