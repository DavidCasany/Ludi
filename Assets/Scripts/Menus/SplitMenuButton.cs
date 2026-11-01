using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Botón de menú que, al pasar el ratón o al llegar con teclado/mando, se divide en varios sub-botones
// (TORNEO -> Continuar / Nuevo,  1 VS 1 -> PvP / PvE).
//
// Va en el GameObject RAÍZ del grupo, que contiene:
//   - el botón principal (Button + CanvasGroup)
//   - un contenedor con los sub-botones (CanvasGroup + Horizontal Layout Group)
//
// El estado se recalcula cada frame (abierto = ratón encima || selección dentro del grupo) en lugar de
// reaccionar evento a evento. Así no parpadea cuando el ratón pasa del botón principal a un sub-botón
// (la salida y la entrada llegan en el mismo frame) y evitamos seleccionar dentro de OnSelect,
// que Unity no permite ("Attempting to select while already selecting").
[DisallowMultipleComponent]
public class SplitMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Button mainButton;
    [SerializeField] CanvasGroup mainGroup;
    [SerializeField] CanvasGroup splitGroup;
    [Tooltip("Sub-botones de izquierda a derecha")]
    [SerializeField] Button[] subButtons;

    [Header("Animación")]
    [SerializeField] float animDuration = 0.12f;
    [Tooltip("Escala horizontal del contenedor cuando está cerrado; al abrirse crece hasta 1")]
    [SerializeField, Range(0.5f, 1f)] float collapsedSplitWidth = 0.8f;

    public Button MainButton => mainButton;
    public bool IsExpanded => expanded;

    MenuPreviewTrigger previewTrigger;
    RectTransform splitRect;
    Selectable navUp, navDown;
    bool hovered, wasHovered, expanded, selectionWasInside;
    float t;   // 0 = cerrado, 1 = abierto

    void Awake()
    {
        previewTrigger = GetComponent<MenuPreviewTrigger>();
        splitRect = (RectTransform)splitGroup.transform;

        // Sin un Graphic en la raíz, el hueco entre sub-botones no recibe el ratón y el grupo parpadea
        if (GetComponent<Graphic>() == null)
        {
            Image hitArea = gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }

        ApplyInteractable();
        ApplyVisual(0f);
    }

    void OnDisable()
    {
        if (selectionWasInside && previewTrigger != null) previewTrigger.SetSelected(false);
        hovered = wasHovered = expanded = selectionWasInside = false;
        t = 0f;
        ApplyInteractable();
        ApplyVisual(0f);
    }

    // Los eventos de puntero suben a los padres: la raíz los recibe aunque el ratón esté sobre un hijo
    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    void Update()
    {
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;

        // Teclado/mando ha llegado al botón principal: se abre y la selección pasa al primer sub-botón
        if (selected != null && selected == mainButton.gameObject)
        {
            Button first = FirstInteractableSub();
            if (first != null)
            {
                es.SetSelectedGameObject(first.gameObject);
                selected = first.gameObject;
            }
        }

        bool selectionInside = IsSubButton(selected);

        // El ratón sale después de haber hecho clic en un sub-botón: soltamos la selección para que se cierre
        if (wasHovered && !hovered && selectionInside)
        {
            es.SetSelectedGameObject(null);
            selectionInside = false;
        }
        wasHovered = hovered;

        bool wantExpanded = hovered || selectionInside;
        if (wantExpanded != expanded)
        {
            expanded = wantExpanded;
            ApplyInteractable();
        }

        // La selección no "sube" a los padres como el hover, así que avisamos nosotros a la imagen de vista previa
        if (selectionInside != selectionWasInside)
        {
            selectionWasInside = selectionInside;
            if (previewTrigger != null) previewTrigger.SetSelected(selectionInside);
        }

        float target = expanded ? 1f : 0f;
        if (t != target)
        {
            t = animDuration > 0f ? Mathf.MoveTowards(t, target, Time.unscaledDeltaTime / animDuration) : target;
            ApplyVisual(t);
        }
    }

    void ApplyInteractable()
    {
        mainGroup.interactable = !expanded;
        mainGroup.blocksRaycasts = !expanded;
        splitGroup.interactable = expanded;
        splitGroup.blocksRaycasts = expanded;
    }

    void ApplyVisual(float x)
    {
        float e = x * x * (3f - 2f * x);   // smoothstep: entra y sale suave
        mainGroup.alpha = 1f - e;
        splitGroup.alpha = e;
        splitRect.localScale = new Vector3(Mathf.Lerp(collapsedSplitWidth, 1f, e), 1f, 1f);
    }

    // ---------- Navegación ----------

    // La llama MainMenuController con la fila de arriba y la de abajo
    public void SetVerticalNeighbours(Selectable up, Selectable down)
    {
        navUp = up;
        navDown = down;
        RefreshNavigation();
    }

    // Vuelve a llamarla si cambias el interactable de un sub-botón en tiempo de ejecución
    public void RefreshNavigation()
    {
        mainButton.navigation = ExplicitNav(navUp, navDown, null, null);

        List<Button> subs = InteractableSubs();
        for (int i = 0; i < subs.Count; i++)
        {
            Selectable left = i > 0 ? subs[i - 1] : null;
            Selectable right = i < subs.Count - 1 ? subs[i + 1] : null;
            subs[i].navigation = ExplicitNav(navUp, navDown, left, right);
        }
    }

    public static Navigation ExplicitNav(Selectable up, Selectable down, Selectable left, Selectable right)
    {
        return new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
    }

    // ---------- Utilidades ----------

    // Se usa .interactable (el del propio botón) y no IsInteractable(), que también tiene en cuenta
    // el CanvasGroup y daría false mientras el grupo está cerrado
    List<Button> InteractableSubs()
    {
        List<Button> list = new List<Button>();
        foreach (Button b in subButtons)
            if (b != null && b.interactable && b.gameObject.activeInHierarchy) list.Add(b);
        return list;
    }

    Button FirstInteractableSub()
    {
        foreach (Button b in subButtons)
            if (b != null && b.interactable && b.gameObject.activeInHierarchy) return b;
        return null;
    }

    bool IsSubButton(GameObject go)
    {
        if (go == null) return false;
        foreach (Button b in subButtons)
            if (b != null && b.gameObject == go) return true;
        return false;
    }
}
