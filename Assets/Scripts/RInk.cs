using UnityEngine;

// Acceso a los límites de la pista (el EdgeCollider2D que ya tiene el GameManager).
public static class Rink
{
    public static bool TryGetBounds(out Bounds bounds)
    {
        bounds = default(Bounds);

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.rinkWalls == null) return false;

        bounds = gm.rinkWalls.bounds;
        return bounds.size.x > 0.1f && bounds.size.y > 0.1f;
    }
}
