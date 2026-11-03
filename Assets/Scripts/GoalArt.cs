using UnityEngine;
using UnityEngine.SceneManagement;

// Cambia el dibujo de las porterías por UIArt.Goal (portería vista desde arriba) sin tocar el prefab:
// al cargar una escena con porterías, cada Goal esconde su SpriteRenderer cuadrado y añade al lado
// un objeto "GoalArt" que ocupa exactamente el mismo rectángulo, girado para que el travesaño quede
// en la línea de gol (hacia el centro de la pista) y la red hacia fuera.
// Las colisiones y la zona de gol no cambian. Si falta la imagen, no hace nada.
public static class GoalArt
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Sprite art = UIArt.Goal;
        if (art == null) return;

        foreach (Goal goal in Object.FindObjectsByType<Goal>(FindObjectsSortMode.None))
        {
            // El dibujo antiguo es un hermano del Goal (prefab Porteria: raíz > Porteria (sprite) + ZonaGol)
            Transform root = goal.transform.parent != null ? goal.transform.parent : goal.transform;

            SpriteRenderer old = null;
            foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.sprite != null && sr.name != "GoalArt") { old = sr; break; }
            if (old == null || !old.enabled) continue;   // sin dibujo o ya cambiado

            Apply(old, art);
        }
    }

    static void Apply(SpriteRenderer old, Sprite art)
    {
        Transform ot = old.transform;

        // Rectángulo del dibujo antiguo en el espacio de su padre: x = profundidad, y = ancho de la boca
        Vector2 oldSize = Vector2.Scale(old.sprite.bounds.size, ot.localScale);
        Vector3 oldCenter = ot.localPosition + Vector3.Scale(old.sprite.bounds.center, ot.localScale);

        GameObject go = new GameObject("GoalArt");
        go.transform.SetParent(ot.parent, false);
        go.transform.localPosition = oldCenter;
        // +90°: lo de arriba del dibujo (el travesaño) mira hacia -x, que es la boca de la portería
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // Tras girar, el ancho del dibujo cubre la boca (y) y el alto cubre la profundidad (x)
        Vector2 artSize = art.bounds.size;
        go.transform.localScale = new Vector3(Mathf.Abs(oldSize.y) / artSize.x, Mathf.Abs(oldSize.x) / artSize.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        sr.sortingLayerID = old.sortingLayerID;
        sr.sortingOrder = old.sortingOrder;

        old.enabled = false;
    }
}
