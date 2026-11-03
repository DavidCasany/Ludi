using UnityEngine;
using UnityEngine.UI;

// Part visual del panell d'un equip a la pantalla de selecció:
//   fons ......... l'escut de l'equip en gran
//   a sobre ...... un vel negre semitransparent (opacitat amb el slider Dim Opacity)
//   davant ....... un jugador de l'equip amb la seva equipació (UIPlayerPreview)
// TeamSidePanel.Show() li diu quin equip ha de mostrar.
public class TeamPreviewVisual : MonoBehaviour
{
    [SerializeField] Image crestBackground;
    [SerializeField] UIPlayerPreview player;

    [Header("Vel negre sobre l'escut")]
    [SerializeField] Image dim;
    [Tooltip("0 = escut sense enfosquir, 1 = negre del tot")]
    [SerializeField, Range(0f, 1f)] float dimOpacity = 0.6f;

    void Awake() => ApplyDim();
    void OnValidate() => ApplyDim();   // en moure el slider a l'Inspector es veu al moment

    void ApplyDim()
    {
        if (dim == null) return;
        Color c = dim.color;
        c.a = dimOpacity;
        dim.color = c;
    }

    // team null = encara no s'ha triat cap equip
    public void Show(TeamData team)
    {
        bool hasCrest = team != null && team.Crest != null;
        if (crestBackground != null)
        {
            crestBackground.enabled = hasCrest;
            if (hasCrest) crestBackground.sprite = team.Crest;
        }

        if (player != null) player.SetKit(team);
    }
}
