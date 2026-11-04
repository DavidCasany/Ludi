using System.Collections.Generic;
using UnityEngine;

// Valores por defecto de los ajustes y lista de resoluciones que se ofrecen.
// El asset debe llamarse "SettingsData" y estar en una carpeta Resources (Assets/Resources/SettingsData.asset)
// para que GameSettings lo encuentre sin referencias. El constructor del menú de ajustes lo crea solo.
[CreateAssetMenu(menuName = "Ludi/Settings Data", fileName = "SettingsData")]
public class SettingsData : ScriptableObject
{
    [Header("Valores por defecto")]
    [Tooltip("La primera vez que se abre el juego usa la resolución nativa del monitor (ignora la de abajo)")]
    [SerializeField] bool defaultToNative = true;
    [SerializeField] Vector2Int defaultResolution = new Vector2Int(1920, 1080);
    [SerializeField] bool defaultFullscreen = true;
    [SerializeField, Range(0f, 1f)] float defaultMusicVolume = 0.05f;
    [SerializeField, Range(0f, 1f)] float defaultSfxVolume = 0.8f;
    [SerializeField] bool defaultPassArrows = true;

    [Header("Controles (móvil / tableta)")]
    [Tooltip("Automàtic: táctil en móviles y tabletas, teclado y ratón en el ordenador (y cambia solo)")]
    [SerializeField] ControlPreference defaultControls = ControlPreference.Auto;
    [SerializeField, Range(GameSettings.TouchScaleMin, GameSettings.TouchScaleMax)] float defaultTouchScale = 1f;
    [SerializeField, Range(GameSettings.TouchOpacityMin, GameSettings.TouchOpacityMax)] float defaultTouchOpacity = 0.7f;
    [Tooltip("Joystick a la derecha y botones a la izquierda")]
    [SerializeField] bool defaultTouchLeftHanded = false;

    [Header("Web")]
    [Tooltip("Resolución interna del canvas: 0 = automática (como mucho x2 la de la pantalla), 1 = nativa, 0.5 = mitad")]
    [SerializeField, Range(0f, 1f)] float defaultRenderScale = 0f;

    [Header("Resoluciones extra del desplegable")]
    [Tooltip("Se suman a las que el monitor dice que soporta (Screen.resolutions). Las que no caben se ocultan")]
    [SerializeField] List<Vector2Int> resolutions = new List<Vector2Int>
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1280, 800),
        new Vector2Int(1366, 768),
        new Vector2Int(1440, 900),
        new Vector2Int(1600, 900),
        new Vector2Int(1680, 1050),
        new Vector2Int(1920, 1080),
        new Vector2Int(1920, 1200),
        new Vector2Int(2560, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(2560, 1600),
        new Vector2Int(3440, 1440),
        new Vector2Int(3840, 2160)
    };

    public bool DefaultToNative => defaultToNative;
    public Vector2Int DefaultResolution => defaultResolution;
    public bool DefaultFullscreen => defaultFullscreen;
    public float DefaultMusicVolume => defaultMusicVolume;
    public float DefaultSfxVolume => defaultSfxVolume;
    public bool DefaultPassArrows => defaultPassArrows;
    public ControlPreference DefaultControls => defaultControls;
    public float DefaultTouchScale => defaultTouchScale;
    public float DefaultTouchOpacity => defaultTouchOpacity;
    public bool DefaultTouchLeftHanded => defaultTouchLeftHanded;
    public float DefaultRenderScale => defaultRenderScale;
    public IReadOnlyList<Vector2Int> Resolutions => resolutions;
}
