using UnityEngine;

// Ajusta un RectTransform a la zona segura de la pantalla (fuera del notch, de las esquinas redondeadas
// y de la barra de gestos de los móviles). Va en un hijo a pantalla completa de un Canvas Screen Space.
// En el navegador Screen.safeArea suele ser la pantalla entera: la página ya deja el canvas dentro de la
// zona segura. En una build nativa de Android/iOS sí recorta.
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("Margen extra (en fracción de la altura) por si el borde del cristal es muy curvo")]
    [Range(0f, 0.05f)] public float extraMargin = 0.01f;

    Rect applied;
    Vector2Int screen;

    void OnEnable() => Apply();

    void Update()
    {
        if (Screen.safeArea != applied || Screen.width != screen.x || Screen.height != screen.y) Apply();
    }

    void Apply()
    {
        RectTransform rt = (RectTransform)transform;
        Rect safe = Screen.safeArea;
        applied = safe;
        screen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;

        float m = extraMargin * Screen.height;
        Vector2 min = new Vector2((safe.xMin + m) / Screen.width, (safe.yMin + m) / Screen.height);
        Vector2 max = new Vector2((safe.xMax - m) / Screen.width, (safe.yMax - m) / Screen.height);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
