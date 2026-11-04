using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Lo que el juego necesita saber del navegador (o del dispositivo, fuera de la web).
// En WebGL llama a Mobile/Plugins/WebGL/LudiWeb.jslib; en el editor y en escritorio devuelve
// valores equivalentes sacados de Unity, así el resto del código no necesita #if.
public static class WebPlatform
{
    public static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int LudiWeb_IsTouchPrimary();
    [DllImport("__Internal")] static extern int LudiWeb_HasTouch();
    [DllImport("__Internal")] static extern int LudiWeb_IsPortrait();
    [DllImport("__Internal")] static extern int LudiWeb_FullscreenSupported();
    [DllImport("__Internal")] static extern float LudiWeb_DevicePixelRatio();
    [DllImport("__Internal")] static extern float LudiWeb_CanvasCssWidth();
    [DllImport("__Internal")] static extern float LudiWeb_CanvasCssHeight();
    [DllImport("__Internal")] static extern void LudiWeb_SetRenderScale(float f);
    [DllImport("__Internal")] static extern void LudiWeb_Init();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        LudiWeb_Init();
#endif
    }

    // ¿El dispositivo se usa principalmente con el dedo? (móvil o tableta; un portátil táctil no)
    public static bool IsTouchPrimary()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return LudiWeb_IsTouchPrimary() == 1;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Application.isMobilePlatform || (Input.touchSupported && !Input.mousePresent);
#else
        return Application.isMobilePlatform;
#endif
    }

    public static bool HasTouch()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return LudiWeb_HasTouch() == 1;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.touchSupported;
#else
        return Application.isMobilePlatform;
#endif
    }

    // Ventana/pantalla en vertical
    public static bool IsPortrait()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return LudiWeb_IsPortrait() == 1;
#else
        return Screen.height > Screen.width;
#endif
    }

    public static bool FullscreenSupported()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return LudiWeb_FullscreenSupported() == 1;
#else
        return !Application.isMobilePlatform;
#endif
    }

    public static float DevicePixelRatio()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Mathf.Max(0.5f, LudiWeb_DevicePixelRatio());
#else
        return 1f;
#endif
    }

    // Tamaño del canvas en píxeles CSS (sin multiplicar por el ratio)
    public static Vector2 CanvasCssSize()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return new Vector2(LudiWeb_CanvasCssWidth(), LudiWeb_CanvasCssHeight());
#else
        return new Vector2(Screen.width, Screen.height);
#endif
    }

    // Ratio que usará el canvas con una escala de render dada (0 = automática). Misma fórmula que el .jslib
    public static float RatioFor(float renderScale)
    {
        float dpr = DevicePixelRatio();
        float r = renderScale > 0f ? dpr * renderScale : Mathf.Min(dpr, 2f);
        return Mathf.Max(0.5f, r);
    }

    // Resolución interna (en píxeles reales) que tendría el canvas con esa escala
    public static Vector2Int ResolutionFor(float renderScale)
    {
        Vector2 css = CanvasCssSize();
        float r = RatioFor(renderScale);
        return new Vector2Int(Mathf.RoundToInt(css.x * r), Mathf.RoundToInt(css.y * r));
    }

    public static void SetRenderScale(float renderScale)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        LudiWeb_SetRenderScale(renderScale);
#endif
    }
}
