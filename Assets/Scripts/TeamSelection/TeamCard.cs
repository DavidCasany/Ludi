using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Tarjeta de un equipo en la rejilla. Solo pinta los datos de su TeamData y avisa de lo que le pasa
// (clic, ratón encima, seleccionada con teclado). Quién la elige lo decide TeamSelectionUI.
public class TeamCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] Button button;
    [SerializeField] Image crest;
    [SerializeField] Image colorStripe;
    [SerializeField] TMP_Text shortNameText;
    [Tooltip("Etiqueta que aparece cuando un lado ha elegido este equipo (TÚ, CPU, J1, J2)")]
    [SerializeField] GameObject badge;
    [SerializeField] Image badgeImage;
    [SerializeField] TMP_Text badgeText;

    public TeamData Team { get; private set; }
    public Button Button => button;

    public event Action<TeamCard> Clicked;
    public event Action<TeamCard> Focused;     // ratón encima o seleccionada con teclado/mando
    public event Action<TeamCard> Unfocused;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    public void Setup(TeamData team)
    {
        Team = team;
        name = "Card_" + team.ShortName;

        shortNameText.text = team.ShortName;
        colorStripe.color = team.PrimaryColor;

        bool hasCrest = team.Crest != null;
        crest.enabled = hasCrest;
        if (hasCrest) crest.sprite = team.Crest;

        SetBadge(null, Color.clear);
    }

    // label null = sin etiqueta
    public void SetBadge(string label, Color color)
    {
        bool show = !string.IsNullOrEmpty(label);
        if (badge != null) badge.SetActive(show);
        if (!show) return;
        if (badgeText != null) badgeText.text = label;
        if (badgeImage != null) badgeImage.color = color;
    }

    public void SetAvailable(bool available) => button.interactable = available;

    public void OnPointerEnter(PointerEventData e) { if (button.interactable) Focused?.Invoke(this); }
    public void OnPointerExit(PointerEventData e) => Unfocused?.Invoke(this);
    public void OnSelect(BaseEventData e) => Focused?.Invoke(this);
    public void OnDeselect(BaseEventData e) => Unfocused?.Invoke(this);
}
