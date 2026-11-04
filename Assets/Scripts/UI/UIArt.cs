using UnityEngine;

// Arte dibujado a mano de la UI y la pista. Se carga de Assets/Resources/Art/ (sin referencias en escena):
//   ButtonFrame.png  marco metálico de los botones (9-slice, ver HockeyArtImporter)
//   UpgradeCard.png  carta de mejora (portería, stick y patín)
//   Goal.png         portería vista desde arriba (travesaño arriba, red hacia abajo)
// Si falta una imagen, quien la usa vuelve al aspecto anterior.
public static class UIArt
{
    static Sprite buttonFrame, upgradeCard, goal, steelPanel, paperPanel;
    static bool loadedButton, loadedCard, loadedGoal, loadedSteel, loadedPaper;

    public static Sprite ButtonFrame => Load(ref buttonFrame, ref loadedButton, "Art/ButtonFrame");
    public static Sprite UpgradeCard => Load(ref upgradeCard, ref loadedCard, "Art/UpgradeCard");
    public static Sprite Goal => Load(ref goal, ref loadedGoal, "Art/Goal");

    // Panells comuns amb l'estil de l'art (9-slice, vora de 64 px). Els genera Tools/gen_ui_sprites.py
    //   SteelPanel: marc metàl·lic amb cargols (com els botons) i interior fosc -> text clar a sobre
    //   PaperPanel: paper crema amb contorn marró (com la targeta) -> text fosc a sobre
    public static Sprite SteelPanel => Load(ref steelPanel, ref loadedSteel, "Art/HUD/ui_steel");
    public static Sprite PaperPanel => Load(ref paperPanel, ref loadedPaper, "Art/HUD/ui_paper");
    public const float PanelSpriteBorder = 64f;

    // Colors de l'art (per a text i detalls sobre aquests panells)
    public static readonly Color Ink = new Color32(45, 56, 64, 255);        // blau nit del patí de la targeta
    public static readonly Color InkBrown = new Color32(114, 89, 73, 255);  // contorn de la targeta
    public static readonly Color Paper = new Color32(243, 234, 218, 255);
    public static readonly Color Steel = new Color32(82, 103, 129, 255);
    public static readonly Color ArtRed = new Color32(167, 68, 59, 255);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        buttonFrame = upgradeCard = goal = steelPanel = paperPanel = null;
        loadedButton = loadedCard = loadedGoal = loadedSteel = loadedPaper = false;
    }

    static Sprite Load(ref Sprite cache, ref bool tried, string path)
    {
        if (cache == null && !tried)
        {
            tried = true;
            cache = Resources.Load<Sprite>(path);
        }
        return cache;
    }

    // Tintes del marco: blanco = el dibujo tal cual; al pasar el ratón, un toque cálido
    public static readonly Color FrameHighlight = new Color(1f, 0.84f, 0.66f);
    public static readonly Color FramePressed = new Color(0.78f, 0.72f, 0.66f);
    public static readonly Color FrameDisabled = new Color(0.42f, 0.44f, 0.48f, 0.95f);

    public static UnityEngine.UI.ColorBlock FrameColors()
    {
        UnityEngine.UI.ColorBlock c = UnityEngine.UI.ColorBlock.defaultColorBlock;
        c.normalColor = Color.white;
        c.highlightedColor = FrameHighlight;
        c.selectedColor = FrameHighlight;
        c.pressedColor = FramePressed;
        c.disabledColor = FrameDisabled;
        c.colorMultiplier = 1f;
        c.fadeDuration = 0.08f;
        return c;
    }
}
