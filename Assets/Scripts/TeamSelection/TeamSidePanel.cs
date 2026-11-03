using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel de un lado (local o visitante): muestra el equipo elegido o el que se está mirando.
// Se puede clicar para pasar a elegir este lado.
public class TeamSidePanel : MonoBehaviour
{
    [SerializeField] Button button;
    [Tooltip("Marco de acento que indica que este lado es el que está eligiendo")]
    [SerializeField] Image activeFrame;
    [Tooltip("Contenido del equipo: se atenúa cuando solo es una vista previa (ratón encima)")]
    [SerializeField] CanvasGroup content;

    [SerializeField] TMP_Text titleText;
    [SerializeField] Image crest;
    [SerializeField] TMP_Text crestFallbackText;   // nombre corto en grande si el equipo no tiene escudo
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text infoText;
    [SerializeField] Image playerColor;
    [SerializeField] Image keeperColor;
    [Tooltip("Opcional: escut de fons, vel negre i jugador amb l'equipació (Tools > Ludi > Afegir escut i jugador...)")]
    [SerializeField] TeamPreviewVisual teamVisual;   // no es diu 'preview': Show() ja té un paràmetre amb aquest nom

    [SerializeField, Range(0f, 1f)] float previewAlpha = 0.55f;

    public Button Button => button;

    public void SetTitle(string title)
    {
        if (titleText != null) titleText.text = title;
    }

    public void SetHighlighted(bool on)
    {
        if (activeFrame != null) activeFrame.enabled = on;
    }

    // team null = nada elegido todavía
    public void Show(TeamData team, bool preview)
    {
        if (content != null) content.alpha = team != null && preview ? previewAlpha : 1f;

        if (team == null)
        {
            crest.enabled = false;
            SetText(crestFallbackText, "?");
            SetText(nameText, "Sin elegir");
            SetText(infoText, "");
            playerColor.color = keeperColor.color = new Color(1f, 1f, 1f, 0.08f);
            if (teamVisual != null) teamVisual.Show(null);
            return;
        }

        // Amb la previsualització, l'escut va de fons i el jugador ocupa el lloc de l'escut petit
        bool hasCrest = team.Crest != null;
        crest.enabled = hasCrest && teamVisual == null;
        if (hasCrest) crest.sprite = team.Crest;
        SetText(crestFallbackText, hasCrest || teamVisual != null ? "" : team.ShortName);
        if (teamVisual != null) teamVisual.Show(team);

        SetText(nameText, team.TeamName);
        SetText(infoText, $"{team.ShortName}  ·  Nivel {team.Rating}");
        playerColor.color = team.PrimaryColor;
        keeperColor.color = team.GoalkeeperColor;
    }

    static void SetText(TMP_Text t, string s)
    {
        if (t != null) t.text = s;
    }
}
