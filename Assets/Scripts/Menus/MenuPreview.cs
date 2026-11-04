using UnityEngine;
using UnityEngine.UI;

// Imagen ilustrativa del menú. Muestra el sprite del botón que tiene el ratón encima; si no hay ninguno,
// el del botón seleccionado con teclado/mando; y si tampoco, la imagen por defecto.
// Hace un fundido entre dos Images (una detrás y otra delante) para que el cambio no sea un corte seco.
// La de detrás se desvanece a la vez y se oculta al acabar: con imágenes con transparencia no se superponen.
public class MenuPreview : MonoBehaviour
{
    [SerializeField] Image backImage;
    [SerializeField] Image frontImage;
    [SerializeField] Sprite defaultSprite;
    [SerializeField] float fadeDuration = 0.15f;

    MenuPreviewTrigger hovered, selected;
    Sprite shown, previous;

    // Opacitat amb què es veu ara una imatge (0 si no es veu). La fan servir els efectes que van
    // a sobre d'una imatge concreta (VsBannerFX, ConfettiBurst) per aparèixer i desaparèixer amb ella.
    public float AlphaOf(Sprite s)
    {
        if (s == null) return 0f;
        if (s == shown) return frontImage.enabled ? frontImage.color.a : 0f;
        if (s == previous && backImage.enabled) return backImage.color.a;
        return 0f;
    }
    bool dirty, fading;
    float backStartAlpha = 1f;

    void Awake()
    {
        shown = defaultSprite;
        SetImage(backImage, null, 0f);      // detrás solo hay algo durante un fundido
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
            SetImage(backImage, null, 0f);  // fundido terminado: la imagen vieja desaparece del todo
        }
        SetAlpha(frontImage, a);
        if (backImage.enabled) SetAlpha(backImage, backStartAlpha * (1f - a));   // la vieja se va mientras entra la nueva
    }

    void StartFade(Sprite next)
    {
        // Lo que se ve ahora (con su transparencia actual, por si se cambia a mitad de un fundido) pasa detrás
        backStartAlpha = frontImage.enabled ? frontImage.color.a : 0f;
        SetImage(backImage, shown, backStartAlpha);
        SetImage(frontImage, next, 0f);    // lo nuevo aparece delante con un fundido
        previous = shown;
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
