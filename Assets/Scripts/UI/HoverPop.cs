using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Crece un poco al pasar el ratón o al seleccionarlo con teclado/mando (cartas, cajas del cuadro...).
// Tiempo real: funciona aunque el juego esté pausado o en cámara lenta.
public class HoverPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] float scale = 1.05f;
    [SerializeField] float duration = 0.1f;

    Selectable selectable;
    bool pointer, selected;
    float t;

    public float Scale { get => scale; set => scale = value; }

    void Awake() => selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData e) => pointer = true;
    public void OnPointerExit(PointerEventData e) => pointer = false;
    public void OnSelect(BaseEventData e) => selected = true;
    public void OnDeselect(BaseEventData e) => selected = false;

    void OnDisable()
    {
        pointer = selected = false;
        t = 0f;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        bool usable = selectable == null || selectable.IsInteractable();
        float target = usable && (pointer || selected) ? 1f : 0f;
        if (Mathf.Approximately(t, target)) return;

        t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
        float s = Mathf.Lerp(1f, scale, t * t * (3f - 2f * t));
        transform.localScale = new Vector3(s, s, 1f);
    }
}
