using UnityEngine;

// Anell neó als peus del jugador que controles (l'indicador que Skater encén i apaga).
// Batega suau (mida i brillantor) amb temps real, perquè es vegi viu també en càmera lenta.
[RequireComponent(typeof(SpriteRenderer))]
public class ControlRing : MonoBehaviour
{
    [SerializeField] Color color = new Color(0.35f, 0.85f, 1f, 1f);   // blau neó (blanc: 1,1,1)
    [SerializeField, Range(0f, 0.2f)] float scalePulse = 0.05f;
    [SerializeField, Range(0f, 1f)] float alphaPulse = 0.3f;
    [SerializeField] float speed = 3f;

    SpriteRenderer sr;
    Vector3 baseScale;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
    }

    void OnValidate()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;
    }

    void Update()
    {
        float w = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);   // 0..1
        transform.localScale = baseScale * (1f + scalePulse * w);

        Color c = color;
        c.a = color.a * (1f - alphaPulse * (1f - w));
        sr.color = c;
    }
}
