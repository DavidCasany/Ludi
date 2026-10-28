using UnityEngine;
using UnityEngine.EventSystems;

// Avisa a MenuPreview de qué imagen enseñar al pasar el ratón o seleccionar con teclado/mando.
//  - En un botón simple (Ajustes, Créditos, Salir): deja el sprite vacío -> se ve la imagen por defecto.
//  - En la raíz de un SplitMenuButton: pon el sprite del modo. El hover le llega desde los hijos y la
//    selección se la pasa el SplitMenuButton, así que NO hace falta ponerlo en los sub-botones.
public class MenuPreviewTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Tooltip("Vacío = imagen por defecto del menú")]
    [SerializeField] Sprite previewSprite;
    [Tooltip("Si se deja vacío, se busca en la escena")]
    [SerializeField] MenuPreview preview;

    public Sprite Sprite => previewSprite;

    void Awake()
    {
        if (preview == null) preview = FindFirstObjectByType<MenuPreview>();
    }

    void OnDisable()
    {
        if (preview == null) return;
        preview.SetHovered(this, false);
        preview.SetSelected(this, false);
    }

    public void OnPointerEnter(PointerEventData eventData) { if (preview != null) preview.SetHovered(this, true); }
    public void OnPointerExit(PointerEventData eventData) { if (preview != null) preview.SetHovered(this, false); }
    public void OnSelect(BaseEventData eventData) => SetSelected(true);
    public void OnDeselect(BaseEventData eventData) => SetSelected(false);

    public void SetSelected(bool on)
    {
        if (preview != null) preview.SetSelected(this, on);
    }
}
