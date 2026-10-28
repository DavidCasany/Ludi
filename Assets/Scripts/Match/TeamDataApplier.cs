using UnityEngine;

// Aplica los datos de un TeamData (colores y nivel de IA) a los jugadores de un TeamManager.
// Misma lógica que TournamentMatchBridge.ApplyTeam, extraída para poder reutilizarla.
public static class TeamDataApplier
{
    public static void Apply(TeamManager tm, TeamData data)
    {
        if (tm == null || data == null) return;

        foreach (Skater s in tm.skaters)
        {
            SpriteRenderer sr = s.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = data.PrimaryColor;

            SkaterAI ai = s.GetComponent<SkaterAI>();
            if (ai != null) ai.skill = data.AiSkill;
        }

        // El portero restaura su color base tras cada parada, así que se lo cambia él mismo
        if (tm.goalkeeper != null) tm.goalkeeper.SetColor(data.GoalkeeperColor);
    }
}
