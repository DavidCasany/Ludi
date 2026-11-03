using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pone el marco metálico (UIArt.ButtonFrame) a los botones de cada escena al cargarla, sin tener que
// tocar las escenas a mano: menú de inicio, selección de equipos, Configuració, Crèdits y el menú de
// pausa dentro del partido (aunque esté oculto). Mismas reglas que la herramienta del editor:
//   - solo botones con texto (los de icono, como el engranaje, se quedan igual),
//   - solo si usan el sprite por defecto de Unity (UISprite) o ninguno: no pisa botones con arte propio,
//   - nunca las tarjetas de equipo.
// Corre antes que los Start de la escena, así DisabledButtonLook ya ve el marco al configurarse.
public static class ButtonArtAutoApply
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (UIArt.ButtonFrame == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Button b in root.GetComponentsInChildren<Button>(true))
                if (Eligible(b)) ButtonFrameFitter.Apply(b);
    }

    public static bool Eligible(Button b)
    {
        Image img = b.targetGraphic as Image;
        if (img == null) return false;
        if (b.GetComponent<TeamCard>() != null) return false;
        if (b.GetComponentInChildren<TMP_Text>(true) == null) return false;   // botón de icono
        Sprite s = img.sprite;
        return s == null || s.name == "UISprite" || s == UIArt.ButtonFrame;
    }
}
