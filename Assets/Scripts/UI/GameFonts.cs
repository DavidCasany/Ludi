using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fonts del joc (Hoquei Maniacs Display i Hoquei Maniacs Text) i els seus estils.
// Els assets els crea Tools > Ludi > Fonts > 1. Crear fonts TextMesh Pro, a Assets/Resources/Fonts/,
// perquè es puguin carregar també des del codi que construeix pantalles en temps d'execució (UIKit).
//
//   Títol .. Display + contorn granat i ombra (títols, marcadors, rellotge, GOL!)
//   Botó ... Display + contorn fosc (etiquetes de botons)
//   Text ... Text (instruccions, descripcions de millores, menús)
//
// Si encara no s'han creat els assets, no fa res: el joc continua amb la font que tingui cada text.
public static class GameFonts
{
    public enum Role { Text, Title, Button }

    public const string Folder = "Fonts/";   // dins de Resources
    public const string DisplayName = "HoqueiManiacs-Display SDF";
    public const string TextName = "HoqueiManiacs-Text SDF";
    public const string TitleMaterial = DisplayName + " - Title";
    public const string ButtonMaterial = DisplayName + " - Button";

    static TMP_FontAsset display, text;
    static Material titleMat, buttonMat;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        display = text = null;
        titleMat = buttonMat = null;
    }

    static void Load()
    {
        if (loaded && display != null) return;
        loaded = true;
        display = Resources.Load<TMP_FontAsset>(Folder + DisplayName);
        text = Resources.Load<TMP_FontAsset>(Folder + TextName);
        titleMat = Resources.Load<Material>(Folder + TitleMaterial);
        buttonMat = Resources.Load<Material>(Folder + ButtonMaterial);
    }

    public static bool Available { get { Load(); return display != null && text != null; } }

    // Endevina el paper d'un text que ja existeix (per aplicar les fonts a escenes fetes)
    public static Role Guess(TMP_Text t)
    {
        string n = t.name.ToLowerInvariant();
        if (t.GetComponentInParent<Button>(true) != null && t.fontSize < 70f) return Role.Button;
        if (t.fontSize >= 56f) return Role.Title;
        foreach (string k in new[] { "title", "titol", "títol", "titulo", "score", "marcador", "clock", "reloj",
                                     "rellotge", "timer", "banner", "header" })
            if (n.Contains(k)) return Role.Title;
        return Role.Text;
    }

    public static void Style(TMP_Text t, Role role)
    {
        if (t == null || !Available) return;

        if (role == Role.Text)
        {
            t.font = text;
            t.fontSharedMaterial = text.material;
            return;
        }

        t.font = display;
        t.fontSharedMaterial = role == Role.Title && titleMat != null ? titleMat
                             : role == Role.Button && buttonMat != null ? buttonMat
                             : display.material;

        // La Display ja és molt gruixuda: res de negreta falsa ni d'espaiat extra
        t.fontStyle &= ~FontStyles.Bold;
        t.characterSpacing = Mathf.Min(t.characterSpacing, 1f);

        // És més ampla que la font d'abans: que s'encongeixi si no hi cap, sense créixer mai
        if (!t.enableAutoSizing)
        {
            float size = t.fontSize;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(8f, size * 0.6f);
        }
    }

    public static void Style(TMP_Text t) => Style(t, Guess(t));
}
