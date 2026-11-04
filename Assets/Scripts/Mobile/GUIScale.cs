using UnityEngine;

// Escala del HUD provisional de OnGUI (marcador, barras, QTE, mensajes...).
// Todo ese HUD está medido en píxeles pensando en 1920×1080. En un móvil o tableta la pantalla tiene
// otra resolución (y en la web el canvas puede ir a menos resolución para ganar rendimiento), así que
// sin escalar se vería diminuto o enorme. Uso, al principio de cada OnGUI:
//
//     GUIScale.Apply();
//     float w = GUIScale.Width, h = GUIScale.Height;   // en lugar de Screen.width / Screen.height
//
// GUI.matrix escala también los clics, así que GUI.Button sigue funcionando sin cambios.
public static class GUIScale
{
    const float RefWidth = 1920f, RefHeight = 1080f;

    // Como un CanvasScaler en modo "Expand": todo lo que cabía a 1920×1080 sigue cabiendo
    public static float Factor => Mathf.Clamp(Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight), 0.35f, 4f);

    // Tamaño de la pantalla en unidades de OnGUI ya escaladas
    public static float Width => Screen.width / Factor;
    public static float Height => Screen.height / Factor;

    public static void Apply()
    {
        float s = Factor;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
    }

    // Píxeles de pantalla (Screen, origen abajo) -> unidades de OnGUI (origen arriba)
    public static Vector2 FromScreen(Vector2 screen)
    {
        float s = Factor;
        return new Vector2(screen.x / s, (Screen.height - screen.y) / s);
    }

    // Rect de OnGUI -> rect en píxeles de pantalla (origen abajo), para compararlo con toques
    public static Rect ToScreenRect(Rect gui)
    {
        float s = Factor;
        return new Rect(gui.x * s, Screen.height - (gui.y + gui.height) * s, gui.width * s, gui.height * s);
    }

    // GUI.Button que además responde a un toque. Según el navegador, un toque llega o no como clic de
    // ratón a OnGUI; así funciona siempre. Solo se mira el toque en Repaint (una vez por frame).
    public static bool Button(Rect r, string text, GUIStyle style)
    {
        bool clicked = GUI.Button(r, text, style);
        if (!clicked && GUI.enabled && Event.current.type == EventType.Repaint &&
            TouchInput.TapEndedIn(ToScreenRect(r)))
            clicked = true;
        return clicked;
    }
}
