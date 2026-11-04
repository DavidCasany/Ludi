using System.Collections;
using UnityEngine;

// "Hit stop": congela el joc unes centèsimes de segon en els cops forts (xut potent, parada, gol) perquè
// pesin, com als jocs de lluita. Es fa servir amb una línia:
//   HitStop.Do(0.08f);
//
// Conviu amb la resta de coses que toquen Time.timeScale (bullet time, QTE del porter, pausa):
//  - només s'atura si el joc va a velocitat normal (en càmera lenta no s'hi suma),
//  - en acabar només restaura el temps si ningú l'ha canviat mentrestant,
//  - si en acabar el joc està en pausa, espera que es reprengui.
public static class HitStop
{
    public static bool Enabled = true;

    static Runner runner;
    static float until;           // temps real en què s'acaba la congelació actual
    static float savedScale = 1f;
    static bool frozen;
    static int generation;        // cada congelació nova invalida l'espera de l'anterior

    public static bool Frozen => frozen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        runner = null;
        until = 0f;
        savedScale = 1f;
        frozen = false;
        generation = 0;
    }

    // Per a qui vol fer-se seu el temps (QTE del porter, que comença en el MATEIX pas de física en què el xut
    // ha congelat el joc): acaba la congelació sense restaurar res i torna la velocitat "de veritat"
    // (la d'abans de congelar), que és la que s'ha de guardar per restaurar després.
    public static float TakeOver()
    {
        if (!frozen) return Time.timeScale;
        frozen = false;
        generation++;
        Time.timeScale = savedScale;
        return savedScale;
    }

    public static void Do(float seconds)
    {
        if (!Enabled || seconds <= 0f || PauseController.IsPaused) return;

        if (frozen)
        {
            until = Mathf.Max(until, Time.unscaledTime + seconds);   // cops seguits: s'allarga, no s'encadena
            return;
        }
        if (Time.timeScale < 0.9f) return;   // ja estem en càmera lenta (apuntant o QTE)

        if (runner == null)
        {
            GameObject go = new GameObject("HitStop");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            runner = go.AddComponent<Runner>();
        }

        savedScale = Time.timeScale;
        until = Time.unscaledTime + seconds;
        frozen = true;
        generation++;
        Time.timeScale = 0f;   // fixedDeltaTime no es toca: la física simplement no avança
        runner.StartCoroutine(Wait(generation));
    }

    static IEnumerator Wait(int gen)
    {
        while (gen == generation && Time.unscaledTime < until) yield return null;
        while (gen == generation && PauseController.IsPaused) yield return null;   // la pausa té prioritat
        if (gen != generation) yield break;   // algú (QTE) s'ha quedat el temps, o n'hi ha una de nova

        // Només si continua aturat per nosaltres (si el bullet time l'ha canviat, és seu)
        if (Time.timeScale == 0f) Time.timeScale = savedScale;
        frozen = false;
    }

    class Runner : MonoBehaviour
    {
        // Si es canvia d'escena a mitja congelació, el joc no es queda parat
        void OnDisable()
        {
            if (frozen && Time.timeScale == 0f && !PauseController.IsPaused) Time.timeScale = savedScale;
            frozen = false;
        }
    }
}
