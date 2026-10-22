using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Herramienta de EDITOR para agrandar la pista sin cambiar el tamaño de los jugadores ni de la bola.
// Cómo usarla:
//   1. Añade este componente a cualquier objeto de la escena (fuera del modo Play).
//   2. Pon el factor (p. ej. 1.5 = una pista un 50 % más grande).
//   3. En el componente, menú de los tres puntos (⋮) > "Aplicar escala". Se puede deshacer con Ctrl+Z.
//   4. Cuando termines, borra el componente (no hay que volver a aplicarlo).
// Escala desde el origen (0,0): posiciones de porterías, jugadores, porteros, saque de centro y el tamaño
// de la pista (RinkBuilder). Asume que el centro de la pista está en el origen.
public class RinkResizer : MonoBehaviour
{
    public float factor = 1.5f;

    [ContextMenu("Aplicar escala")]
    void Apply()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Debug.LogWarning("RinkResizer: sal del modo Play antes de aplicar la escala.");
            return;
        }

        int moved = 0;

        foreach (Goal g in FindObjectsByType<Goal>(FindObjectsSortMode.None)) { ScalePosition(g.transform); moved++; }
        foreach (Goalkeeper k in FindObjectsByType<Goalkeeper>(FindObjectsSortMode.None)) { ScalePosition(k.transform); moved++; }
        foreach (Skater s in FindObjectsByType<Skater>(FindObjectsSortMode.None)) { ScalePosition(s.transform); moved++; }

        GameManager gm = FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            Undo.RecordObject(gm, "Escalar pista");
            gm.ballStart = gm.ballStart * factor;
            EditorUtility.SetDirty(gm);
        }

        foreach (RinkBuilder rb in FindObjectsByType<RinkBuilder>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(rb, "Escalar pista");
            rb.width *= factor;
            rb.height *= factor;
            rb.Build();
            EditorUtility.SetDirty(rb);
        }

        Debug.Log("RinkResizer: pista escalada x" + factor + " (" + moved + " objetos movidos). " +
                  "Revisa que las porterías y los porteros queden bien y borra este componente.");
#endif
    }

#if UNITY_EDITOR
    void ScalePosition(Transform t)
    {
        Undo.RecordObject(t, "Escalar pista");
        Vector3 p = t.position;
        t.position = new Vector3(p.x * factor, p.y * factor, p.z);
        EditorUtility.SetDirty(t);
    }
#endif
}
