using System.Collections.Generic;
using UnityEngine;

// BoxCollider2D con "Is Trigger" activado, colocado dentro de la red (detrás de la línea de gol).
// Además define la ZONA PROHIBIDA para los jugadores: el collider de la red + 'keepOutPadding'.
// Asegúrate de que el collider cubre toda la red (el área donde los jugadores no deben entrar).
[RequireComponent(typeof(Collider2D))]
public class Goal : MonoBehaviour
{
    public static readonly List<Goal> All = new List<Goal>();

    public Team defendingTeam; // equipo que defiende esta portería

    // Semiancho real de la boca de la portería (lo usan el portero y la IA)
    public float HalfWidth => Col.bounds.extents.y;
    
    [Tooltip("Margen extra alrededor del collider de la red que los jugadores no pueden pisar")]
    public float keepOutPadding = 0.3f;

    Collider2D col;

    // Acceso perezoso: otros scripts pueden llamar a TryPushOut antes de que se ejecute nuestro Awake
    Collider2D Col => col != null ? col : (col = GetComponent<Collider2D>());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void Awake() { col = GetComponent<Collider2D>(); }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<Ball>() == null) return;

        Team scorer = defendingTeam == Team.Home ? Team.Away : Team.Home;
        if (GameManager.Instance.State == MatchState.Playing) GameCamera.Shake(0.5f, 0.45f);   // el gol cuenta: sacudida
        GameManager.Instance.GoalScored(scorer);
    }

    public void SwapDefendingTeam()
    {
        defendingTeam = defendingTeam == Team.Home ? Team.Away : Team.Home;
    }

    // Centro de la BOCA de la portería: el punto medio de la cara frontal del collider (sobre la línea de gol).
    // Hay que apuntar aquí, no al transform de la red: ese suele estar detrás de la línea.
    public Vector2 Mouth
    {
        get
        {
            Bounds b = Col.bounds;
            float frontX = b.center.x > 0f ? b.min.x : b.max.x;   // la cara que mira al centro (x = 0)
            return new Vector2(frontX, b.center.y);
        }
    }

    // Portería más cercana a un punto (para traducir el Transform de TeamManager a una Goal)
    public static Goal Nearest(Vector2 p)
    {
        Goal best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < All.Count; i++)
        {
            float d = ((Vector2)All[i].Col.bounds.center - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = All[i]; }
        }
        return best;
    }

    // Caja de la zona prohibida (red + padding + 'extra'). La usa GoalNav para rodear la portería.
    public Bounds KeepOutBounds(float extra)
    {
        Bounds b = Col.bounds;
        b.Expand(2f * (keepOutPadding + extra));
        return b;
    }

    // Si 'p' está dentro de la zona prohibida (red + padding + 'extra'), lo empuja fuera y devuelve true.
    // Sale por la cara más cercana: frente, espalda o laterales (antes no existía la espalda y un jugador
    // que llegaba por detrás de la red podía "atravesarla" hacia el frente).
    // 'normal' = dirección en la que se ha sacado (para anular la velocidad hacia dentro).
    public bool TryPushOut(ref Vector2 p, float extra, out Vector2 normal)
    {
        normal = Vector2.zero;

        Bounds b = Col.bounds;
        float pad = keepOutPadding + extra;
        float minX = b.min.x - pad, maxX = b.max.x + pad;
        float minY = b.min.y - pad, maxY = b.max.y + pad;

        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY) return false;

        float frontX = b.center.x > 0f ? -1f : 1f;               // el frente mira al centro de la pista (x = 0)
        float dFront = frontX > 0f ? maxX - p.x : p.x - minX;
        float dBack = frontX > 0f ? p.x - minX : maxX - p.x;
        float dBottom = p.y - minY;
        float dTop = maxY - p.y;

        float m = Mathf.Min(Mathf.Min(dFront, dBack), Mathf.Min(dBottom, dTop));

        if (m == dFront)
        {
            p.x = frontX > 0f ? maxX : minX;
            normal = new Vector2(frontX, 0f);
        }
        else if (m == dBack)
        {
            p.x = frontX > 0f ? minX : maxX;
            normal = new Vector2(-frontX, 0f);
        }
        else if (m == dBottom)
        {
            p.y = minY;
            normal = Vector2.down;
        }
        else
        {
            p.y = maxY;
            normal = Vector2.up;
        }
        return true;
    }
}