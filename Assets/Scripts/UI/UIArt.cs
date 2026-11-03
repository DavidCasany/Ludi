using UnityEngine;

// Arte dibujado a mano de la UI y la pista. Se carga de Assets/Resources/Art/ (sin referencias en escena):
//   ButtonFrame.png  marco metálico de los botones (9-slice, ver HockeyArtImporter)
//   UpgradeCard.png  carta de mejora (portería, stick y patín)
//   Goal.png         portería vista desde arriba (travesaño arriba, red hacia abajo)
// Si falta una imagen, quien la usa vuelve al aspecto anterior.
public static class UIArt
{
    static Sprite buttonFrame, upgradeCard, goal;
    static bool loadedButton, loadedCard, loadedGoal;

    public static Sprite ButtonFrame => Load(ref buttonFrame, ref loadedButton, "Art/ButtonFrame");
    public static Sprite UpgradeCard => Load(ref upgradeCard, ref loadedCard, "Art/UpgradeCard");
    public static Sprite Goal => Load(ref goal, ref loadedGoal, "Art/Goal");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        buttonFrame = upgradeCard = goal = null;
        loadedButton = loadedCard = loadedGoal = false;
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
