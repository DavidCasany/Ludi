using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Joystick virtual "flotante": va en una zona grande e invisible (media pantalla). Donde se pone el dedo
// aparece la base y el pomo sigue al dedo. Si el dedo se aleja más que el radio, la base lo persigue,
// así nunca se "pierde" el joystick. Sin dedo vuelve a su sitio de reposo (semitransparente).
//
// Value: dirección x fuerza (magnitud 0..1), ya con zona muerta. Es lo que TeamManager usa como WASD.
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler,
                               IInitializePotentialDragHandler
{
    public float radius = 120f;                         // recorrido del pomo (unidades del canvas)
    [Range(0f, 0.5f)] public float deadZone = 0.12f;
    [Tooltip("Fuerza a partir de la cual se considera 'a fondo' (el resto de recorrido da 1)")]
    [Range(0.5f, 1f)] public float fullAt = 0.85f;
    public bool followFinger = true;

    public RectTransform Base { get; set; }
    public RectTransform Knob { get; set; }
    public Vector2 RestPosition { get; set; }           // posición de la base sin dedo (local a la zona)

    public Vector2 Value { get; private set; }
    public bool Active => pointerId != int.MinValue;

    int pointerId = int.MinValue;
    Vector2 center;
    RectTransform zone;
    Canvas canvas;
    CanvasGroup baseGroup;

    void Awake() => zone = (RectTransform)transform;

    public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

    public void OnPointerDown(PointerEventData e)
    {
        if (Active) return;
        pointerId = e.pointerId;
        center = ToLocal(e);
        // Que la base no quede medio fuera de la zona si se toca muy pegado al borde
        Rect r = zone.rect;
        center.x = Mathf.Clamp(center.x, r.xMin + radius * 0.6f, r.xMax - radius * 0.6f);
        center.y = Mathf.Clamp(center.y, r.yMin + radius * 0.6f, r.yMax - radius * 0.6f);
        UpdateKnob(ToLocal(e));
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        UpdateKnob(ToLocal(e));
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != pointerId) return;
        ResetState();
    }

    void OnDisable() => ResetState();

    public void ResetState()
    {
        pointerId = int.MinValue;
        Value = Vector2.zero;
        center = RestPosition;
        Place(Vector2.zero);
    }

    void UpdateKnob(Vector2 finger)
    {
        Vector2 d = finger - center;
        float m = d.magnitude;
        if (followFinger && m > radius)
        {
            center += d / m * (m - radius);   // la base persigue al dedo
            d = finger - center;
            m = radius;
        }

        Vector2 offset = m > radius ? d / m * radius : d;
        float t = Mathf.Clamp01(offset.magnitude / radius);
        // Zona muerta y "a fondo" un poco antes del borde: el dedo no tiene que estirarse tanto
        float strength = t <= deadZone ? 0f : Mathf.Clamp01((t - deadZone) / Mathf.Max(0.01f, fullAt - deadZone));
        Value = t > 0f ? offset.normalized * strength : Vector2.zero;
        Place(offset);
    }

    void Place(Vector2 knobOffset)
    {
        if (Base != null) Base.anchoredPosition = center;
        if (Knob != null) Knob.anchoredPosition = knobOffset;
        if (baseGroup == null && Base != null) baseGroup = Base.GetComponent<CanvasGroup>();
        if (baseGroup != null) baseGroup.alpha = Active ? 1f : 0.55f;
    }

    Vector2 ToLocal(PointerEventData e)
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, cam, out local);
        return local;
    }
}
