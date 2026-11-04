using UnityEngine;

// Base dels efectes que van a sobre d'UNA imatge concreta del MenuPreview (VsBannerFX, ConfettiBurst).
//  - Apareixen i desapareixen amb la imatge (mateixa opacitat, via CanvasGroup).
//  - Saben on cau un punt de la imatge (coordenades 0..1 del sprite) encara que l'Image
//    tingui Preserve Aspect i la imatge no ompli tot el rectangle.
// Tot funciona amb temps real (Time.unscaledDeltaTime): el menú pot estar amb el temps aturat.
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public abstract class MenuPreviewFX : MonoBehaviour
{
    [SerializeField] protected MenuPreview preview;
    [Tooltip("La imatge del menú sobre la qual va aquest efecte")]
    [SerializeField] protected Sprite forSprite;

    protected RectTransform rt;
    CanvasGroup group;

    protected float Visibility { get; private set; }
    protected bool IsVisible => Visibility > 0.01f;

    protected virtual void Awake()
    {
        rt = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        if (preview == null) preview = GetComponentInParent<MenuPreview>();
    }

    protected virtual void LateUpdate()
    {
        Visibility = preview != null ? preview.AlphaOf(forSprite) : 0f;
        group.alpha = Visibility;
    }

    // Rectangle (en coordenades locals) on l'Image amb Preserve Aspect dibuixa el sprite
    protected Rect DrawnRect()
    {
        Rect r = rt.rect;
        if (forSprite == null || r.width <= 0f || r.height <= 0f) return r;
        float aspect = forSprite.rect.width / forSprite.rect.height;
        float w = r.width, h = r.height;
        if (w / h > aspect) w = h * aspect;
        else h = w / aspect;
        return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
    }

    // Punt de la imatge (u, v de 0 a 1, origen a baix a l'esquerra) en coordenades locals
    protected Vector2 ImagePoint(Vector2 uv)
    {
        Rect d = DrawnRect();
        return new Vector2(d.x + uv.x * d.width, d.y + uv.y * d.height);
    }
}
