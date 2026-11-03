using UnityEngine;

// Aplica los datos de un TeamData (equipación con patrón y nivel de IA) a los jugadores de un TeamManager.
// Lo usan MatchBootstrapper (amistoso) y TournamentMatchBridge (torneo).
public static class TeamDataApplier
{
    // Por debajo de esta distancia de color los dos equipos se confundirían: el visitante usa la 2ª equipación
    const float ClashThreshold = 0.3f;

    // Aplica los dos equipos de un partido resolviendo el choque de colores
    public static void ApplyMatch(TeamManager home, TeamData homeData, TeamManager away, TeamData awayData)
    {
        if (homeData == null || awayData == null) return;

        bool clash = ColorDistance(homeData.PrimaryColor, awayData.PrimaryColor) < ClashThreshold;

        Apply(home, homeData, false);
        Apply(away, awayData, clash);
    }

    // alternateKit = segunda equipación: camiseta AlternateColor y el patrón con el color principal
    // (p. ej. camiseta blanca con las franjas del color del club)
    public static void Apply(TeamManager tm, TeamData data, bool alternateKit = false)
    {
        if (tm == null || data == null) return;

        Color shirt = alternateKit ? data.AlternateColor : data.PrimaryColor;
        Color patternColor = alternateKit ? data.PrimaryColor : data.SecondaryColor;
        Sprite pattern = alternateKit ? data.AlternatePattern : data.JerseyPattern;

        foreach (Skater s in tm.skaters)
        {
            SkaterVisual v = s.GetComponent<SkaterVisual>();
            if (v != null)
            {
                v.SetKit(shirt, data.SecondaryColor, pattern, patternColor);
            }
            else
            {
                // Jugador sin capas (prefab antiguo): se tiñe el sprite entero, como antes
                SpriteRenderer sr = s.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = shirt;
            }

            SkaterAI ai = s.GetComponent<SkaterAI>();
            if (ai != null) ai.skill = data.AiSkill;
        }

        // El portero restaura su color base tras cada parada, así que se lo cambia él mismo
        if (tm.goalkeeper != null) tm.goalkeeper.SetColor(data.GoalkeeperColor);
    }

    // Distancia de color ponderada (el ojo nota más el verde que el azul)
    static float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
        return Mathf.Sqrt(0.3f * dr * dr + 0.59f * dg * dg + 0.11f * db * db);
    }
}
