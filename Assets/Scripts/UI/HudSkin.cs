using UnityEngine;

// Estil de l'art per a la interfície que encara es dibuixa amb OnGUI (barres del jugador, QTE del porter,
// textos flotants). Fa servir les mateixes peces que els menús, en versió petita:
//   Assets/Resources/Art/HUD/ui_steel_s, ui_paper_s (vora 28 px), ui_track, ui_fill (vora 9 px)
// Si falten, torna als rectangles plans d'abans.
public static class HudSkin
{
    public static readonly Color Ink = new Color32(45, 56, 64, 255);
    public static readonly Color Paper = new Color32(243, 234, 218, 255);
    public static readonly Color Green = new Color32(118, 182, 104, 255);   // zona bona del QTE, esprint
    public static readonly Color Red = new Color32(214, 96, 82, 255);       // esgotat, fallada
    public static readonly Color Yellow = new Color32(236, 196, 92, 255);   // bullet time, regat
    public static readonly Color Orange = new Color32(242, 120, 43, 255);   // accent dels menús

    static GUIStyle steel, paper, track, fill;
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        steel = paper = track = fill = null;
        loaded = false;
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        steel = Box("ui_steel_s", 28);
        paper = Box("ui_paper_s", 28);
        track = Box("ui_track", 9);
        fill = Box("ui_fill", 9);
    }

    static GUIStyle Box(string name, int border)
    {
        Texture2D tex = Resources.Load<Texture2D>("Art/HUD/" + name);
        if (tex == null) return null;
        GUIStyle s = new GUIStyle();
        s.normal.background = tex;
        s.border = new RectOffset(border, border, border, border);
        return s;
    }

    // Les vores d'OnGUI es dibuixen a mida real: si el rectangle és més petit que dues vores, es reduïx
    static void Draw(GUIStyle s, Rect r, Color tint, Color fallback)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color old = GUI.color;
        if (s == null)
        {
            GUI.color = fallback * new Color(1f, 1f, 1f, tint.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
            return;
        }
        int b = s.border.left;
        int fit = Mathf.FloorToInt(Mathf.Min(r.width, r.height) * 0.5f);
        RectOffset saved = null;
        if (fit < b)
        {
            saved = s.border;
            s.border = new RectOffset(fit, fit, fit, fit);
        }
        GUI.color = tint;
        s.Draw(r, false, false, false, false);
        GUI.color = old;
        if (saved != null) s.border = saved;
    }

    public static void SteelPanel(Rect r, float alpha = 1f)
    {
        Load();
        Draw(steel, r, new Color(1f, 1f, 1f, alpha), new Color(0.12f, 0.16f, 0.22f, 0.85f));
    }

    public static void PaperPanel(Rect r, float alpha = 1f)
    {
        Load();
        Draw(paper, r, new Color(1f, 1f, 1f, alpha), Paper);
    }

    // Barra arrodonida: fons fosc amb vora d'acer i farciment del color indicat
    public static void Bar(Rect r, float value01, Color color, float alpha = 1f)
    {
        Load();
        Draw(track, r, new Color(1f, 1f, 1f, alpha), new Color(0f, 0f, 0f, 0.6f));
        float w = (r.width - 4f) * Mathf.Clamp01(value01);
        if (w < 1f) return;
        Rect f = new Rect(r.x + 2f, r.y + 2f, Mathf.Max(w, Mathf.Min(r.height - 4f, r.width - 4f)), r.height - 4f);
        Draw(fill, f, new Color(color.r, color.g, color.b, color.a * alpha), color);
    }

    // Text en negreta amb un contorn fosc de 1 px (es llegeix sobre la pista i sobre l'acer)
    public static void OutlinedLabel(Rect r, string text, GUIStyle style, Color color, float alpha = 1f)
    {
        Color old = style.normal.textColor;
        style.normal.textColor = new Color(0.08f, 0.1f, 0.14f, 0.85f * alpha);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * 1.5f, r.y + dy * 1.5f, r.width, r.height), text, style);
        style.normal.textColor = new Color(color.r, color.g, color.b, color.a * alpha);
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }
}
