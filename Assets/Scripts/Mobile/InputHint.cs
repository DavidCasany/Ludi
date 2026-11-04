using TMPro;
using UnityEngine;

// Texto de ayuda que depende del esquema de control ("Enter · Continuar" no sirve con el dedo).
// Guarda el texto de teclado que tenía el TMP y, en modo táctil, pone el alternativo
// o, si no hay alternativo, oculta el objeto. Cambia en directo si cambia InputMode.
//
//   InputHint.Attach(hintText, "Toca una carta");   // texto táctil
//   InputHint.Attach(hintText, null);               // en táctil se oculta
[DisallowMultipleComponent]
public class InputHint : MonoBehaviour
{
    [SerializeField, TextArea] string touchText;

    TMP_Text label;
    string keyboardText;
    bool hiddenByUs;

    public static InputHint Attach(TMP_Text text, string touchText)
    {
        if (text == null) return null;
        InputHint h = text.GetComponent<InputHint>();
        if (h == null) h = text.gameObject.AddComponent<InputHint>();
        h.label = text;
        h.keyboardText = text.text;
        h.touchText = touchText;
        h.Apply(InputMode.Current);
        return h;
    }

    void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label != null && keyboardText == null) keyboardText = label.text;
        InputMode.Changed += Apply;
    }

    void OnDestroy() => InputMode.Changed -= Apply;

    void Start() => Apply(InputMode.Current);

    void Apply(ControlScheme scheme)
    {
        if (label == null) return;
        bool touch = scheme == ControlScheme.Touch;

        if (touch && string.IsNullOrEmpty(touchText))
        {
            // Ocultar el texto sin desactivar el objeto: así este componente sigue escuchando
            label.enabled = false;
            hiddenByUs = true;
            return;
        }
        if (hiddenByUs) { label.enabled = true; hiddenByUs = false; }
        label.text = touch ? touchText : keyboardText;
    }
}
