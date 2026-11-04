using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Diálogo de confirmación genérico, construido por código en su propio Canvas (por encima de todo).
//   ConfirmDialog.Show("¿Empezar un torneo nuevo?", "Se perderá el torneo en curso.", "EMPEZAR", "CANCELAR", onYes);
// Por defecto queda seleccionado "Cancelar": un Enter sin querer no borra nada. Esc = cancelar.
public class ConfirmDialog : MonoBehaviour
{
    public static bool IsOpen => current != null;
    static ConfirmDialog current;

    Action onYes, onNo;
    Button noButton;

    public static ConfirmDialog Show(string title, string message, string yesLabel, string noLabel,
                                     Action yes, Action no = null)
    {
        if (current != null) Destroy(current.gameObject);

        Canvas canvas = UIKit.CreateCanvas("ConfirmDialog", 500);
        ConfirmDialog d = canvas.gameObject.AddComponent<ConfirmDialog>();
        d.onYes = yes;
        d.onNo = no;
        d.Build((RectTransform)canvas.transform, title, message, yesLabel, noLabel);
        current = d;
        return d;
    }

    void Build(RectTransform root, string title, string message, string yesLabel, string noLabel)
    {
        Image dim = UIKit.Image(root, "Dim", UIKit.Alpha(Color.black, 0.65f), 0f, true);
        UIKit.Stretch(dim.rectTransform, 0f);

        Image card = UIKit.SteelPanel(root, "Card", 36f, true);
        UIKit.Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 380f));

        Image bar = UIKit.Image(card.transform, "Accent", UIKit.Accent, 3f);
        UIKit.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(90f, 6f));

        TextMeshProUGUI t = UIKit.Text(card.transform, "Title", title, 42f, UIKit.TextMain, FontStyles.Bold);
        UIKit.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(800f, 60f));

        TextMeshProUGUI m = UIKit.Text(card.transform, "Message", message, 26f, UIKit.TextSoft);
        m.textWrappingMode = TextWrappingModes.Normal;
        UIKit.Place(m.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(760f, 100f));

        Button yes = UIKit.Button(card.transform, "Yes", yesLabel, true, 28f);
        UIKit.Place((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-160f, 40f), new Vector2(290f, 84f));
        yes.onClick.AddListener(Yes);

        noButton = UIKit.Button(card.transform, "No", noLabel, false, 28f);
        UIKit.Place((RectTransform)noButton.transform, new Vector2(0.5f, 0f), new Vector2(160f, 40f), new Vector2(290f, 84f));
        noButton.onClick.AddListener(No);

        Navigation nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = noButton };
        yes.navigation = nav;
        noButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = yes };
        UIKit.Select(noButton);
    }

    void Update()
    {
        if (UIKit.BackPressed()) No();
    }

    void Yes() => Close(onYes);
    void No() => Close(onNo);

    void Close(Action action)
    {
        if (current == this) current = null;
        Destroy(gameObject);
        action?.Invoke();
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }
}
