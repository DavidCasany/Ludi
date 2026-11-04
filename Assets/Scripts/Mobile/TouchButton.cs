using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón redondo de los controles táctiles. A diferencia de un Button de Unity:
//  - se puede MANTENER (Held) y se sabe cuándo se ha pulsado aunque se lea más tarde (ConsumePress);
//  - cada botón sigue a SU dedo, así que se pueden pulsar varios a la vez con el joystick;
//  - opcionalmente funciona como mini-joystick (Drag): arrastrar el dedo desde el botón apunta.
// Lo construye TouchControls; no hace falta montarlo a mano.
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler,
                           IInitializePotentialDragHandler
{
    [Tooltip("Arrastrar el dedo desde el botón da una dirección (apuntar el tiro)")]
    public bool dragAim;
    [Tooltip("Distancia (unidades del canvas) a partir de la cual el arrastre cuenta como dirección")]
    public float dragDeadZone = 28f;
    public float dragMaxDistance = 150f;
    [Tooltip("Una pulsación no leída caduca tras este tiempo (s reales), para que no se dispare tarde")]
    public float pressMemory = 0.3f;

    public Image Face { get; private set; }
    public TextMeshProUGUI Label { get; private set; }
    public RectTransform Thumb { get; set; }   // punto que sigue al dedo al apuntar (opcional)

    public bool Held { get; private set; }

    // Dirección y fuerza (0..1) del arrastre mientras se mantiene; cero dentro de la zona muerta
    public Vector2 Drag { get; private set; }

    Color baseColor;
    int pointerId = int.MinValue;
    Vector2 pressLocal;
    float pressTime = -100f;
    bool pressPending;
    RectTransform rect;
    Canvas canvas;

    public void Setup(Image face, TextMeshProUGUI label, Color color)
    {
        Face = face;
        Label = label;
        baseColor = color;
        face.color = color;
        rect = (RectTransform)transform;
    }

    public void SetColor(Color c)
    {
        baseColor = c;
        if (Face != null) Face.color = Held ? Pressed(c) : c;
    }

    // true una sola vez por pulsación (la lee TeamManager en su Update, que puede ir antes o después
    // del EventSystem en el mismo frame)
    public bool ConsumePress()
    {
        if (!pressPending) return false;
        pressPending = false;
        return Time.unscaledTime - pressTime <= pressMemory;
    }

    public void ResetState()
    {
        Held = false;
        pressPending = false;
        pointerId = int.MinValue;
        Drag = Vector2.zero;
        ApplyVisual();
    }

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

    public void OnPointerDown(PointerEventData e)
    {
        if (Held) return;   // ya lo pulsa otro dedo
        pointerId = e.pointerId;
        Held = true;
        pressPending = true;
        pressTime = Time.unscaledTime;
        Drag = Vector2.zero;
        ApplyVisual();             // primero la escala de "pulsado": el arrastre se mide ya con ella
        pressLocal = ToLocal(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (!Held || e.pointerId != pointerId || !dragAim) return;
        Vector2 d = ToLocal(e) - pressLocal;
        float m = d.magnitude;
        Drag = m < dragDeadZone ? Vector2.zero : d / m * Mathf.Clamp01(m / dragMaxDistance);
        ApplyVisual();
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        Held = false;
        pointerId = int.MinValue;
        ApplyVisual();
        // Drag se mantiene hasta la siguiente pulsación: TeamManager lo lee al soltar para tirar
    }

    void OnDisable() => ResetState();

    Vector2 ToLocal(PointerEventData e)
    {
        if (rect == null) rect = (RectTransform)transform;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, cam, out local);
        return local;
    }

    void ApplyVisual()
    {
        if (Face != null) Face.color = Held ? Pressed(baseColor) : baseColor;
        transform.localScale = Held ? new Vector3(0.93f, 0.93f, 1f) : Vector3.one;

        if (Thumb != null)
        {
            bool show = Held && dragAim && Drag.sqrMagnitude > 0f;
            Thumb.gameObject.SetActive(show);
            if (show)
            {
                float r = rect.rect.width * 0.5f;
                Thumb.anchoredPosition = Drag * r;
            }
        }
    }

    static Color Pressed(Color c) => new Color(Mathf.Min(1f, c.r + 0.18f), Mathf.Min(1f, c.g + 0.18f),
                                               Mathf.Min(1f, c.b + 0.18f), Mathf.Min(1f, c.a + 0.2f));
}
