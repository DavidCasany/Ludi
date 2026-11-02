using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Una "camiseta" de la alineación: dorsal, nombre, frase y rol. Entra con un pequeño salto y, con el
// ratón encima, se endereza y crece un poco. Los datos los pone CreditsUI.
public class CreditCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image band;
    [SerializeField] TMP_Text numberText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text taglineText;
    [SerializeField] TMP_Text roleText;
    [SerializeField] CanvasGroup group;

    [Header("Animación")]
    [SerializeField] float introDuration = 0.45f;
    [SerializeField] float introTilt = -10f;
    [SerializeField] float hoverScale = 1.04f;

    float intro = 1f;     // 0 = escondida, 1 = en su sitio
    bool playing;
    float hover;          // 0..1
    bool hovered;
    float restTilt;       // inclinación final (cada carta un poco distinta, como colgadas)

    public void Setup(CreditsUI.Member m, float tilt)
    {
        band.color = m.color;
        numberText.text = m.number.ToString();
        nameText.text = m.fullName;
        taglineText.text = m.tagline;
        taglineText.color = m.color;
        roleText.text = m.role;
        restTilt = tilt;
        intro = 0f;
        playing = false;
        Apply();
    }

    public void PlayIntro() => playing = true;

    public void Finish()
    {
        playing = false;
        intro = 1f;
        Apply();
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) => hovered = false;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool changed = false;

        if (playing && intro < 1f)
        {
            intro = Mathf.MoveTowards(intro, 1f, dt / introDuration);
            if (intro >= 1f) playing = false;
            changed = true;
        }

        float h = Mathf.MoveTowards(hover, hovered ? 1f : 0f, dt / 0.12f);
        if (h != hover) { hover = h; changed = true; }

        if (changed) Apply();
    }

    void Apply()
    {
        float e = EaseOutBack(intro);
        float s = Mathf.LerpUnclamped(0.6f, 1f, e) * Mathf.Lerp(1f, hoverScale, hover);
        transform.localScale = new Vector3(s, s, 1f);

        float tilt = Mathf.LerpUnclamped(introTilt, restTilt, e) * (1f - hover);   // con el ratón encima, recta
        transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        if (group != null) group.alpha = Mathf.Clamp01(intro * 2.5f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
