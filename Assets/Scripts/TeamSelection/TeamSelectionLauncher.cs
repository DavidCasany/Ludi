using UnityEngine;

// Lo único que sale de la pantalla de selección hacia el partido: rellena un MatchSetup y llama a
// MatchLauncher.Launch (que ya carga la escena Match). Sin estado propio y sin DontDestroyOnLoad.
public static class TeamSelectionLauncher
{
    // PvE: el jugador es el local, la máquina el visitante. Un amistoso puede acabar en empate.
    public static bool PlayPvE(TeamData playerTeam, TeamData cpuTeam, string matchScene = MatchLauncher.DefaultMatchScene)
    {
        if (!CheckTeams(playerTeam, cpuTeam)) return false;

        MatchSetup setup = new MatchSetup
        {
            homeTeam = playerTeam,
            awayTeam = cpuTeam,
            homeController = ControllerType.HumanP1,
            awayController = ControllerType.CPU,
            requireWinner = false
        };
        MatchLaunch.Mode = MatchMode.PvE;   // la pausa lo lee para el texto de contexto
        return MatchLauncher.Launch(setup, matchScene);
    }

    // PvP: preparado. Mientras el partido no tenga input del jugador 2, HumanP2 juega como CPU (aviso amarillo).
    public static bool PlayPvP(TeamData p1Team, TeamData p2Team, string matchScene = MatchLauncher.DefaultMatchScene)
    {
        if (!CheckTeams(p1Team, p2Team)) return false;

        MatchSetup setup = new MatchSetup
        {
            homeTeam = p1Team,
            awayTeam = p2Team,
            homeController = ControllerType.HumanP1,
            awayController = ControllerType.HumanP2,
            requireWinner = false
        };
        MatchLaunch.Mode = MatchMode.PvP;
        return MatchLauncher.Launch(setup, matchScene);
    }

    // Mismo equipo en los dos lados = mismos colores: no se distinguirían en la pista
    static bool CheckTeams(TeamData a, TeamData b)
    {
        if (a == null || b == null)
        {
            Debug.LogError("[TeamSelection] Faltan equipos para lanzar el partido.");
            return false;
        }
        if (a == b)
        {
            Debug.LogError("[TeamSelection] Los dos lados no pueden ser el mismo equipo.");
            return false;
        }
        return true;
    }
}
