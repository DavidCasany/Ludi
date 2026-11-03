using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel de elección de mejoras (roguelite): título, cartas de la oferta actual y atajos 1/2/3.
// Lee y escribe en TournamentSession; cuando ya no quedan elecciones pendientes lanza Completed.
// Lo usan la pantalla de resultado del partido (lo normal) y, como red de seguridad, el cuadro
// (si se cerró el juego a mitad de elección o el torneo da mejoras antes del primer partido).
public class UpgradeDraftPanel : MonoBehaviour
{
    const float CardWidth = 330f;
    const float CardHeight = 430f;
    const float CardGap = 36f;

    public event Action Completed;

    TextMeshProUGUI titleText, subtitleText;
    RectTransform cardRow;
    readonly List<Button> cards = new List<Button>();
    bool done;

    public bool IsDone => done;

    // Crea el panel ocupando todo 'parent' (el que llama decide dónde va y qué hay detrás)
    public static UpgradeDraftPanel Create(Transform parent)
    {
        RectTransform root = UIKit.Rect(parent, "UpgradeDraft");
        UIKit.Stretch(root, 0f);
        UpgradeDraftPanel panel = root.gameObject.AddComponent<UpgradeDraftPanel>();
        panel.Build(root);
        return panel;
    }

    void Build(RectTransform root)
    {
        titleText = UIKit.Text(root, "Title", "ELIGE UNA MEJORA", 46f, UIKit.TextMain, FontStyles.Bold);
        titleText.characterSpacing = 6f;
        UIKit.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(1400f, 60f));

        subtitleText = UIKit.Text(root, "Subtitle", "", 24f, UIKit.TextSoft);
        UIKit.Place(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1400f, 34f));

        cardRow = UIKit.Rect(root, "Cards");
        UIKit.Place(cardRow, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, CardHeight));
        HorizontalLayoutGroup h = cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = CardGap;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;

        TextMeshProUGUI hint = UIKit.Text(root, "Hint", "Clic en una carta  ·  o pulsa 1, 2, 3", 20f, UIKit.TextFaint);
        UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -136f - CardHeight),
                    new Vector2(1000f, 30f));
    }

    // Empieza (o retoma) la tanda de elecciones que le tocan al jugador
    public void Begin()
    {
        TournamentSession.BeginPickRound();
        done = false;
        Refresh();
    }

    void Update()
    {
        if (done) return;
        int n = UIKit.NumberPressed();
        if (n > 0 && n <= cards.Count) Choose(n - 1);
    }

    void Refresh()
    {
        foreach (Button b in cards)
        {
            if (b == null) continue;
            b.gameObject.SetActive(false);   // fuera del layout ya este frame (Destroy espera al final)
            Destroy(b.gameObject);
        }
        cards.Clear();

        int pending = TournamentSession.PlayerPicksPending;
        IReadOnlyList<int> offer = pending > 0 ? TournamentSession.PlayerOffer : (IReadOnlyList<int>)Array.Empty<int>();
        pending = TournamentSession.PlayerPicksPending;   // pedir la oferta puede saltar elecciones sin mejoras

        if (pending <= 0 || offer.Count == 0)
        {
            done = true;
            Completed?.Invoke();
            return;
        }

        int total = Mathf.Max(pending, TournamentSession.PlayerPicksThisRound);
        int current = total - pending + 1;
        titleText.text = total > 1 ? $"ELIGE UNA MEJORA  ·  {current} DE {total}" : "ELIGE UNA MEJORA";
        subtitleText.text = total > 1
            ? "Has avanzado sin esperar: recuperas las jornadas que te has saltado"
            : "Todos los equipos eligen una mejora por jornada (los rivales también)";

        for (int i = 0; i < offer.Count; i++)
        {
            UpgradeData u = TournamentSession.Upgrade(offer[i]);
            if (u == null) continue;
            int index = i;
            Button card = CreateCard(cardRow, u, i + 1);
            card.onClick.AddListener(() => Choose(index));
            cards.Add(card);
        }

        // Con teclado/mando: la primera carta queda seleccionada
        if (cards.Count > 0 && UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == null)
            UIKit.Select(cards[0]);
    }

    void Choose(int offerIndex)
    {
        if (done) return;
        TournamentSession.ChoosePlayerUpgrade(offerIndex);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        Refresh();
    }

    // ---------- Carta ----------

    static Button CreateCard(Transform parent, UpgradeData u, int number)
    {
        Color rc = RarityColor(u.Rarity);

        // Marco del color de la rareza + cuerpo
        Image frame = UIKit.Image(parent, "Card_" + u.DisplayName, Color.white, 18f, true);
        frame.rectTransform.sizeDelta = new Vector2(CardWidth, CardHeight);
        Button button = frame.gameObject.AddComponent<Button>();
        button.targetGraphic = frame;
        button.colors = UIKit.Colors(UIKit.Alpha(rc, 0.55f), rc, rc);
        frame.gameObject.AddComponent<HoverPop>().Scale = 1.06f;

        Image body = UIKit.Image(frame.transform, "Body", UIKit.Panel, 15f);
        UIKit.Stretch(body.rectTransform, 4f);

        // Cabecera: rareza y atajo
        TextMeshProUGUI rarity = UIKit.Text(body.transform, "Rarity", UpgradeData.RarityName(u.Rarity), 18f, rc,
                                            FontStyles.Bold, TextAlignmentOptions.Left);
        rarity.characterSpacing = 8f;
        UIKit.PlaceTopLeft(rarity.rectTransform, 22f, 18f, 200f, 26f);

        Image key = UIKit.Image(body.transform, "Key", UIKit.Surface, 8f);
        UIKit.Place(key.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(36f, 34f));
        TextMeshProUGUI keyText = UIKit.Text(key.transform, "Number", number.ToString(), 20f, UIKit.TextSoft,
                                             FontStyles.Bold);
        UIKit.Stretch(keyText.rectTransform, 0f);

        // Icono (o un círculo con la inicial mientras no haya arte)
        Image iconBg = UIKit.CircleImage(body.transform, "Icon", UIKit.Alpha(rc, 0.18f), 104f);
        UIKit.Place(iconBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(104f, 104f));
        if (u.Icon != null)
        {
            Image icon = UIKit.Image(iconBg.transform, "Sprite", Color.white);
            icon.sprite = u.Icon;
            icon.preserveAspect = true;
            UIKit.Stretch(icon.rectTransform, 16f);
        }
        else
        {
            string initial = string.IsNullOrEmpty(u.DisplayName) ? "?" : u.DisplayName.Substring(0, 1).ToUpper();
            TextMeshProUGUI t = UIKit.Text(iconBg.transform, "Initial", initial, 52f, rc, FontStyles.Bold);
            UIKit.Stretch(t.rectTransform, 0f);
        }

        TextMeshProUGUI title = UIKit.Text(body.transform, "Name", u.DisplayName, 30f, UIKit.TextMain, FontStyles.Bold);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.enableAutoSizing = true;
        title.fontSizeMin = 20f;
        title.fontSizeMax = 30f;
        UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(CardWidth - 40f, 72f));

        Image divider = UIKit.Image(body.transform, "Divider", UIKit.Alpha(rc, 0.5f));
        UIKit.Place(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -254f), new Vector2(70f, 3f));

        TextMeshProUGUI effects = UIKit.Text(body.transform, "Effects", ColoredEffects(u), 21f, UIKit.TextMain);
        effects.textWrappingMode = TextWrappingModes.Normal;
        effects.richText = true;
        effects.lineSpacing = 8f;
        effects.enableAutoSizing = true;
        effects.fontSizeMin = 15f;
        effects.fontSizeMax = 21f;
        effects.alignment = TextAlignmentOptions.Top;
        UIKit.Place(effects.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(CardWidth - 44f, 92f));

        if (!string.IsNullOrEmpty(u.FlavorText))
        {
            TextMeshProUGUI flavor = UIKit.Text(body.transform, "Flavor", u.FlavorText, 16f, UIKit.TextFaint,
                                                FontStyles.Italic);
            flavor.textWrappingMode = TextWrappingModes.Normal;
            flavor.enableAutoSizing = true;
            flavor.fontSizeMin = 12f;
            flavor.fontSizeMax = 16f;
            UIKit.Place(flavor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(CardWidth - 44f, 52f));
        }
        return button;
    }

    // Una línea por efecto: verde si ayuda, rojo si es un coste
    public static string ColoredEffects(UpgradeData u)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < u.Modifiers.Count; i++)
        {
            StatModifier m = u.Modifiers[i];
            if (i > 0) sb.Append('\n');
            sb.Append(m.IsBeneficial ? "<color=#7CFF8A>" : "<color=#FF8080>").Append(m.Describe()).Append("</color>");
        }
        return sb.ToString();
    }

    public static Color RarityColor(UpgradeRarity r)
    {
        switch (r)
        {
            case UpgradeRarity.Epica: return new Color(0.75f, 0.45f, 1f);
            case UpgradeRarity.Rara: return new Color(0.35f, 0.65f, 1f);
            default: return new Color(0.65f, 0.7f, 0.78f);
        }
    }
}
