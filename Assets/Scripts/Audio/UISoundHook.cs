using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sonidos de un elemento de interfaz. NO hace falta añadirlo a mano: AudioManager se lo pone
// automáticamente a cada Button / Toggle / Slider / Dropdown que aparece en pantalla.
//
//  - Pasar el ratón por encima ............ ui_hover
//  - Clic en un botón ..................... ui_click
//      · texto VOLVER / ATRÁS / CANCELAR / CERRAR / SALIR / MENÚ / NO ... ui_back
//      · texto JUGAR / REVANCHA ........... ui_play_match
//      · cartas de mejora ................. (sin clic: suena la elección según su rareza)
//  - Clic en algo desactivado ............. ui_deny
//  - Mover un slider ...................... ui_slider
//
// Si queréis que un botón concreto use otro sonido, añadidle este componente a mano y
// elegid 'Override' en el Inspector.
[DisallowMultipleComponent]
public class UISoundHook : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public enum Override { Auto, Click, Back, PlayMatch, Silent }

    [SerializeField] Override clickSound = Override.Auto;

    Selectable selectable;
    float enabledAt;

    static readonly string[] BackWords = { "VOLVER", "ATRAS", "CANCELAR", "CERRAR", "SALIR", "MENU", "BACK", "TORNAR", "CANCEL", "TANCAR", "SORTIR" };
    static readonly string[] PlayWords = { "JUGAR", "REVANCHA", "REVENJA" };
    static readonly string[] ResumeWords = { "CONTINUAR", "REANUDAR", "REPRENDRE" };

    public void Init(Selectable s)
    {
        if (selectable != null || s == null) return;   // ya configurado (Awake o AudioManager)
        selectable = s;
        Button button = s as Button;
        Slider slider = s as Slider;
        if (button != null) button.onClick.AddListener(OnButtonClicked);
        else if (slider != null) slider.onValueChanged.AddListener(OnSliderChanged);
    }

    void Awake()
    {
        // Si se ha añadido a mano desde el Inspector, se configura solo
        if (selectable == null)
        {
            Selectable s = GetComponent<Selectable>();
            if (s != null) Init(s);
        }
    }

    void OnEnable() { enabledAt = Time.unscaledTime; }

    bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TouchInput.IsTouchPointer(eventData)) return;   // con el dedo "entrar" es tocar: ya suena el clic
        if (Interactable && clickSound != Override.Silent) AudioManager.PlayUI("ui_hover", 0.04f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!Interactable)
        {
            AudioManager.PlayUI("ui_deny");
            return;
        }
        // Los botones suenan con onClick (así también suenan con teclado o mando); el resto aquí
        if (!(selectable is Button) && !(selectable is Slider)) AudioManager.PlayUI("ui_click", 0.03f);
    }

    void OnSliderChanged(float value)
    {
        // Los menús colocan el valor guardado al abrirse: eso no debe sonar
        if (Time.unscaledTime - enabledAt < 0.3f) return;
        AudioManager.PlayUI("ui_slider", 0.08f);
    }

    void OnButtonClicked()
    {
        switch (clickSound)
        {
            case Override.Silent: return;
            case Override.Click: AudioManager.PlayUI("ui_click", 0.03f); return;
            case Override.Back: AudioManager.PlayUI("ui_back"); return;
            case Override.PlayMatch: AudioManager.PlayUI("ui_play_match"); return;
        }

        // Cartas de mejora: suena la elección (AudioManager, según la rareza), no el clic
        if (GetComponentInParent<UpgradeDraftPanel>() != null) return;

        string label = Label();
        // CONTINUAR en la pausa: ya suena ui_pause_close al reanudar
        if (PauseController.IsPaused && ContainsAny(label, ResumeWords)) return;

        if (ContainsAny(label, PlayWords)) AudioManager.PlayUI("ui_play_match");
        else if (label == "NO" || ContainsAny(label, BackWords)) AudioManager.PlayUI("ui_back");
        else AudioManager.PlayUI("ui_click", 0.03f);
    }

    // Texto del botón (o nombre del objeto) en mayúsculas y sin acentos
    string Label()
    {
        string text = "";
        TMP_Text tmp = GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) text = tmp.text;
        if (string.IsNullOrEmpty(text))
        {
            Text legacy = GetComponentInChildren<Text>(true);
            if (legacy != null) text = legacy.text;
        }
        if (string.IsNullOrEmpty(text)) text = gameObject.name;
        return AudioManager.Key(StripTags(text)).ToUpperInvariant();
    }

    static string StripTags(string s)
    {
        // Quita etiquetas de texto enriquecido (<b>, <color=...>)
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        bool inTag = false;
        foreach (char c in s)
        {
            if (c == '<') inTag = true;
            else if (c == '>') inTag = false;
            else if (!inTag) sb.Append(c);
        }
        return sb.ToString();
    }

    static bool ContainsAny(string text, string[] words)
    {
        foreach (string w in words)
            if (text.Contains(w)) return true;
        return false;
    }
}
