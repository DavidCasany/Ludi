using UnityEngine;

// Navegación alrededor de las porterías. Si la línea recta hasta el objetivo atraviesa la zona prohibida
// de una portería, devuelve una esquina de esa zona por la que rodearla (la de recorrido total más corto).
// Se recalcula cada frame, así que el jugador va encadenando esquinas hasta tener vía libre.
public static class GoalNav
{
    const float CornerPad = 0.3f;

    public static Vector2 Route(Vector2 from, Vector2 to, float clearance)
    {
        for (int i = 0; i < Goal.All.Count; i++)
        {
            Bounds b = Goal.All[i].KeepOutBounds(clearance);
            Vector2 min = b.min, max = b.max;

            if (Inside(from, min, max)) continue;            // Skater.KeepOutOfGoals ya lo saca
            if (!SegmentHits(from, to, min, max)) continue;  // vía libre respecto a esta portería

            Vector2 best = to;
            float bestCost = float.MaxValue;
            float detour = (max - min).magnitude;

            for (int c = 0; c < 4; c++)
            {
                Vector2 corner = new Vector2(c % 2 == 0 ? min.x - CornerPad : max.x + CornerPad,
                                             c < 2 ? min.y - CornerPad : max.y + CornerPad);

                if (SegmentHits(from, corner, min, max)) continue;   // esa esquina no se ve desde aquí

                float cost = Vector2.Distance(from, corner) + Vector2.Distance(corner, to);
                if (SegmentHits(corner, to, min, max)) cost += detour; // aún habrá que rodear otra esquina
                if (cost < bestCost) { bestCost = cost; best = corner; }
            }
            return best;
        }
        return to;
    }

    // ¿La recta a-b cruza la caja? (método de los "slabs")
    public static bool SegmentHits(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
    {
        Vector2 d = b - a;
        float t0 = 0f, t1 = 1f;

        for (int axis = 0; axis < 2; axis++)
        {
            float p = axis == 0 ? a.x : a.y;
            float dd = axis == 0 ? d.x : d.y;
            float lo = axis == 0 ? min.x : min.y;
            float hi = axis == 0 ? max.x : max.y;

            if (Mathf.Abs(dd) < 1e-6f)
            {
                if (p <= lo || p >= hi) return false;
                continue;
            }

            float ta = (lo - p) / dd;
            float tb = (hi - p) / dd;
            if (ta > tb) { float tmp = ta; ta = tb; tb = tmp; }

            t0 = Mathf.Max(t0, ta);
            t1 = Mathf.Min(t1, tb);
            if (t0 >= t1) return false;
        }
        return true;
    }

    static bool Inside(Vector2 p, Vector2 min, Vector2 max)
    {
        return p.x > min.x && p.x < max.x && p.y > min.y && p.y < max.y;
    }
}
