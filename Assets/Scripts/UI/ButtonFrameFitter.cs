using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Pone el marco metálico (UIArt.ButtonFrame) a un botón y lo mantiene proporcionado a su altura:
//  - el 9-slice se escala para que el marco entero quepa en la altura del botón (tornillos sin deformar),
//  - el texto se aparta de la decoración de la derecha para quedar centrado en la zona gris.
// Funciona también en el editor (ExecuteAlways), así se ve bien al retocar tamaños a mano.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ButtonFrameFitter : MonoBehaviour
{
    [Tooltip("Texto del botón (se busca solo si está vacío)")]
    [SerializeField] TMP_Text label;
    [Tooltip("Margen extra del texto dentro de la zona gris, en píxeles del botón")]
    [SerializeField] float textPadding = 6f;

    Image image;
    RectTransform rt;
    float lastHeight = -1f;

    // Le pone el marco a un botón (lo usan UIKit y la herramienta del editor)
    public static ButtonFrameFitter Apply(Button button)
    {
        Sprite frame = UIArt.ButtonFrame;
        Image img = button != null ? button.targetGraphic as Image : null;
        if (frame == null || img == null) return null;

        img.sprite = frame;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.color = Color.white;
        button.colors = UIArt.FrameColors();

        ButtonFrameFitter f = img.GetComponent<ButtonFrameFitter>();
        if (f == null) f = img.gameObject.AddComponent<ButtonFrameFitter>();
        f.Refresh(true);
        return f;
    }

    void OnEnable() => Refresh(true);
    void OnRectTransformDimensionsChange() => Refresh(false);

    void Refresh(bool force)
    {
        if (image == null) image = GetComponent<Image>();
        if (rt == null) rt = (RectTransform)transform;
        if (image == null || image.sprite == null) return;

        float h = rt.rect.height;
        if (h < 1f || (!force && Mathf.Approximately(h, lastHeight))) return;
        lastHeight = h;

        // Multiplicador > 1 = bordes más pequeños. Con alto_sprite / alto_botón, el marco ocupa justo la altura.
        Sprite s = image.sprite;
        float mult = s.rect.height / h;
        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, mult)) image.pixelsPerUnitMultiplier = mult;

        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        Vector4 b = s.border;                                   // izquierda, abajo, derecha, arriba
        RectTransform lt = label.rectTransform;
        // Solo se escribe si cambia: en el editor, reescribir lo mismo marcaría la escena como modificada
        Vector2 min = new Vector2(b.x / mult * 0.6f + textPadding, lt.offsetMin.y);
        Vector2 max = new Vector2(-(b.z / mult * 0.75f + textPadding), lt.offsetMax.y);
        if (lt.anchorMin != Vector2.zero) lt.anchorMin = Vector2.zero;
        if (lt.anchorMax != Vector2.one) lt.anchorMax = Vector2.one;
        if ((lt.offsetMin - min).sqrMagnitude > 0.01f) lt.offsetMin = min;
        if ((lt.offsetMax - max).sqrMagnitude > 0.01f) lt.offsetMax = max;
    }
}
