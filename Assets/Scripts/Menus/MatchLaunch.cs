using UnityEngine;

public enum MatchMode { PvE, PvP, Tournament }

// Lo que se le dice a la escena de partido al cargarla (estático, como TournamentSession).
//  - MainMenuController pone PvP o PvE
//  - TournamentHub pone Tournament justo antes de cargar el partido
// La escena de partido es la misma en todos los casos: TournamentMatchBridge, PauseMenuUI, etc.
// leen MatchLaunch.Mode para saber en qué modo están.
public static class MatchLaunch
{
    public static MatchMode Mode { get; set; } = MatchMode.PvE;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Mode = MatchMode.PvE;
}
