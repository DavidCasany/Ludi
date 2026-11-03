using UnityEngine;
using UnityEngine.UI;

// Jugador d'un equip dins de la UI (p. ex. la pantalla de selecció).
// Fa servir les MATEIXES capes que el jugador del partit (Assets/Sprites/Player/), però com a Images,
// i les pinta amb l'equipació del TeamData: samarreta, patró i guants.
// Té una animació d'espera suau (respira i balanceja l'estic) amb temps real, perquè funcioni encara
// que el joc estigui en pausa (Time.timeScale = 0).
public class UIPlayerPreview : MonoBehaviour
{
    [Header("Capes que es tenyeixen")]
    [SerializeField] Image jersey;
    [SerializeField] Image pattern;
    [SerializeField] Image gloves;

    [Header("Animació")]
    [SerializeField] RectTransform body;            // tot el jugador
    [SerializeField] RectTransform[] stickParts;    // estic i guants (giren junts sobre el centre del cos)
    [Tooltip("Gir del jugador perquè no surti completament recte (graus)")]
    [SerializeField] float facingAngle = -20f;
    [SerializeField] float swayAngle = 10f;
    [SerializeField] float swaySpeed = 1.6f;
    [SerializeField, Range(0f, 0.05f)] float breathe = 0.015f;

    public void SetKit(TeamData team)
    {
        gameObject.SetActive(team != null);
        if (team == null) return;

        if (jersey != null) jersey.color = team.PrimaryColor;
        if (gloves != null) gloves.color = team.SecondaryColor;

        if (pattern != null)
        {
            pattern.sprite = team.JerseyPattern;
            pattern.color = team.SecondaryColor;
            pattern.enabled = team.JerseyPattern != null;   // sense patró: samarreta llisa (i no un quadrat blanc)
        }
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (body != null)
        {
            body.localRotation = Quaternion.Euler(0f, 0f, facingAngle);
            body.localScale = Vector3.one * (1f + Mathf.Sin(t * swaySpeed * 2f) * breathe);
        }

        if (stickParts == null) return;
        Quaternion sway = Quaternion.Euler(0f, 0f, Mathf.Sin(t * swaySpeed) * swayAngle);
        foreach (RectTransform part in stickParts)
            if (part != null) part.localRotation = sway;
    }
}
