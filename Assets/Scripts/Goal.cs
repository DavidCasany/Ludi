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

    [Tooltip("Margen extra alrededor del collider de la red que los jugadores no pueden pisar")]
    public float keepOutPadding = 0.3f;

    Collider2D col;

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

    // Si 'p' está dentro de la zona prohibida (red + padding + 'extra'), lo empuja fuera y devuelve true.
    // Sale por el frente (hacia el centro de la pista, x = 0) salvo que esté más cerca de un lateral.
    // 'normal' = dirección en la que se ha sacado (para anular la velocidad hacia dentro).
    public bool TryPushOut(ref Vector2 p, float extra, out Vector2 normal)
    {
        normal = Vector2.zero;

        Bounds b = col.bounds;
        float pad = keepOutPadding + extra;
        float minX = b.min.x - pad, maxX = b.max.x + pad;
        float minY = b.min.y - pad, maxY = b.max.y + pad;

        if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY) return false;

        float frontX = b.center.x > 0f ? -1f : 1f;               // el frente mira al centro de la pista
        float dFront = frontX > 0f ? maxX - p.x : p.x - minX;
        float dBottom = p.y - minY;
        float dTop = maxY - p.y;

        if (dFront <= dBottom && dFront <= dTop)
        {
            p.x = frontX > 0f ? maxX : minX;
            normal = new Vector2(frontX, 0f);
        }
        else if (dBottom < dTop)
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
