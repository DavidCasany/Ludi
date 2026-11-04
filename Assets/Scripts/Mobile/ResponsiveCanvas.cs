using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Todas las pantallas del juego están diseñadas a 1920×1080 con CanvasScaler "Match Width Or Height = 0.5".
// En 16:9 da igual, pero en otros formatos se sale contenido:
//   - móvil 19.5:9 (2340×1080): con 0.5 el lienzo mide 2120×978 -> falta altura y los menús se pisan;
//   - tableta 4:3 (2048×1536): el lienzo mide 1663×1247 -> falta anchura y se cortan los laterales.
// Con "Expand" el lienzo siempre mide AL MENOS 1920×1080: todo lo que cabía en 16:9 cabe en cualquier
// formato, y lo que sobra es aire. Se aplica solo al cargar cada escena (no hace falta reconstruirlas).
// Los canvas que ya usan otro criterio a propósito (p. ej. TouchControls, que va por altura) no se tocan.
public static class ResponsiveCanvas
{
    static readonly Vector2 Reference = new Vector2(1920f, 1080f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (CanvasScaler s in root.GetComponentsInChildren<CanvasScaler>(true))
                Apply(s);
    }

    // Solo los escaladores "de serie" del proyecto: 1920×1080 y Match Width Or Height a 0.5
    public static void Apply(CanvasScaler s)
    {
        if (s == null || s.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return;
        if (s.referenceResolution != Reference) return;
        if (s.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight) return;
        if (!Mathf.Approximately(s.matchWidthOrHeight, 0.5f)) return;
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
    }
}
