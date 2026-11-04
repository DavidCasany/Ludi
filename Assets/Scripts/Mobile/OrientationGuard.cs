using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El juego es apaisado. En modo táctil, si el móvil/tableta se pone en vertical:
//  - en pleno partido se pausa (para no perder la jugada mientras se gira),
//  - se tapa la pantalla con "Gira el dispositiu".
// En la web la página ya enseña su propio aviso a pantalla completa (la plantilla web); aquí solo se
// dibuja si el propio canvas está en vertical (otra página, build nativa...). Siempre activo: se crea solo.
public class OrientationGuard : MonoBehaviour
{
    const float CheckInterval = 0.25f;   // s reales

    GameObject overlay;
    bool portrait;
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        GameObject go = new GameObject("[OrientationGuard]") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(go);
        go.AddComponent<OrientationGuard>();
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + CheckInterval;

        bool now = InputMode.IsTouch && WebPlatform.IsPortrait();
        if (now != portrait)
        {
            portrait = now;
            if (portrait) PauseMatch();
        }

        bool canvasPortrait = InputMode.IsTouch && Screen.height > Screen.width;
        if (canvasPortrait && overlay == null) BuildOverlay();
        if (overlay != null && overlay.activeSelf != canvasPortrait) overlay.SetActive(canvasPortrait);
    }

    static void PauseMatch()
    {
        if (PauseController.IsPaused) return;
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.State == MatchState.FullTime) return;
        PauseController pc = FindFirstObjectByType<PauseController>();
        if (pc != null) pc.Pause();
    }

    void BuildOverlay()
    {
        Canvas c = UIKit.CreateCanvas("RotateDevice", 30000);   // por encima de todo
        c.transform.SetParent(transform, false);                // vive con este objeto (no se destruye al cambiar de escena)
        CanvasScaler scaler = c.GetComponent<CanvasScaler>();
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;  // en vertical manda el ancho
        overlay = c.gameObject;

        Image bg = UIKit.Image(c.transform, "Background", UIKit.Background, 0f, true);   // bloquea los toques
        UIKit.Stretch(bg.rectTransform, 0f);

        // Dibujo: el móvil en vertical (apagado) y en horizontal (naranja)
        Image tall = UIKit.Image(c.transform, "PhoneTall", UIKit.Alpha(UIKit.TextFaint, 0.5f), 28f);
        UIKit.Place(tall.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 190f), new Vector2(150f, 260f));
        Image wide = UIKit.Image(c.transform, "PhoneWide", UIKit.Accent, 28f);
        UIKit.Place(wide.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(130f, 190f), new Vector2(260f, 150f));

        TextMeshProUGUI t = UIKit.Text(c.transform, "Text", "GIRA EL DISPOSITIU", 90f, UIKit.TextMain, FontStyles.Bold);
        t.enableAutoSizing = true;
        t.fontSizeMin = 30f;
        t.fontSizeMax = 90f;
        UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1700f, 120f));

        TextMeshProUGUI sub = UIKit.Text(c.transform, "Sub", "El joc es juga en horitzontal", 50f, UIKit.TextSoft);
        UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(1700f, 80f));
    }
}
