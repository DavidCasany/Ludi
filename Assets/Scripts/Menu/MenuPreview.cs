using UnityEngine;
using UnityEngine.UI;

// Imagen ilustrativa del menú. Muestra el sprite del botón que tiene el ratón encima; si no hay ninguno,
// el del botón seleccionado con teclado/mando; y si tampoco, la imagen por defecto.
// Hace un fundido entre dos Images (una detrás y otra delante) para que el cambio no sea un corte seco.
public class MenuPreview : MonoBehaviour
{
    [SerializeField] Image backImage;
    [SerializeField] Image frontImage;
    [SerializeField] Sprite defaultSprite;
    [SerializeField] float fadeDuration = 0.15f;

    MenuPreviewTrigger hovered, selected;
    Sprite shown;
    bool dirty, fading;

    void Awake()
    {
        shown = defaultSprite;
        SetImage(backImage, defaultSprite, 1f);
        SetImage(frontImage, defaultSprite, 1f);
    }

    public void SetHovered(MenuPreviewTrigger trigger, bool on)
    {
        if (on) hovered = trigger;
        else if (hovered == trigger) hovered = null;
        dirty = true;
    }

    public void SetSelected(MenuPreviewTrigger trigger, bool on)
    {
        if (on) selected = trigger;
        else if (selected == trigger) selected = null;
        dirty = true;
    }

    // Se resuelve en LateUpdate: si en un mismo frame salimos de un botón y entramos en otro,
    // solo se aplica el resultado final (sin un parpadeo a la imagen por defecto entre medias)
    void LateUpdate()
    {
        if (dirty)
        {
            dirty = false;
            MenuPreviewTrigger source = hovered != null ? hovered : selected;
            Sprite wanted = source != null && source.Sprite != null ? source.Sprite : defaultSprite;
            if (wanted != shown) StartFade(wanted);
        }

        if (!fading) return;

        float a = frontImage.color.a + (fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f);
        if (a >= 1f)
        {
            a = 1f;
            fading = false;
        }
        SetAlpha(frontImage, a);
    }

    void StartFade(Sprite next)
    {
        SetImage(backImage, shown, 1f);    // lo que se veía pasa detrás
        SetImage(frontImage, next, 0f);    // lo nuevo aparece delante con un fundido
        shown = next;
        fading = true;
    }

    static void SetImage(Image img, Sprite sprite, float alpha)
    {
        img.sprite = sprite;
        img.enabled = sprite != null;      // una Image sin sprite se vería como un rectángulo blanco
        SetAlpha(img, alpha);
    }

    static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}
