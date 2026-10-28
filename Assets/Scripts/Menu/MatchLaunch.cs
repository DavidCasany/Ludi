using UnityEngine;

public enum MatchMode { PvE, PvP }

// Lo que el menú le dice a la escena de partido al cargarla (estático, como TournamentSession).
// Si al final PvP y PvE comparten escena, el partido lee MatchLaunch.Mode para decidir si el
// segundo equipo lo controla un humano o la IA.
public static class MatchLaunch
{
    public static MatchMode Mode { get; set; } = MatchMode.PvE;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Mode = MatchMode.PvE;
}
