using System.Collections.Generic;
using UnityEngine;

// Configuración de un torneo. Crear: Create > Hockey > Torneo.
[CreateAssetMenu(menuName = "Hockey/Torneo", fileName = "Tournament_")]
public class TournamentConfig : ScriptableObject
{
    [SerializeField] string tournamentName = "Copa Ludi";

    [Tooltip("4, 8 o 16 equipos. El orden es el de cabeza de serie (el primero es el favorito).")]
    [SerializeField] List<TeamData> teams = new List<TeamData>();
    [Tooltip("El equipo del jugador. Debe estar también en la lista de equipos.")]
    [SerializeField] TeamData playerTeam;
    [Tooltip("Sortea el cuadro en vez de usar el orden de la lista")]
    [SerializeField] bool shuffleSeeds = true;

    [Header("Escenas (deben estar en File > Build Profiles)")]
    [SerializeField] string hubScene = "Tournament";
    [SerializeField] string matchScene = "Match";

    [Header("Simulación de partidos IA vs IA")]
    [SerializeField, Range(0.5f, 8f)] float goalsPerTeam = 3f;

    public string TournamentName => tournamentName;
    public IReadOnlyList<TeamData> Teams => teams;
    public TeamData PlayerTeam => playerTeam;
    public bool ShuffleSeeds => shuffleSeeds;
    public string HubScene => hubScene;
    public string MatchScene => matchScene;
    public float GoalsPerTeam => goalsPerTeam;

    public bool IsValid(out string error)
    {
        error = null;
        if (!DoubleEliminationBracket.IsValidSize(teams.Count)) error = "Hacen falta 4, 8 o 16 equipos (hay " + teams.Count + ")";
        else if (teams.Contains(null)) error = "Hay huecos vacíos en la lista de equipos";
        else if (playerTeam == null || !teams.Contains(playerTeam)) error = "El equipo del jugador debe estar en la lista";
        return error == null;
    }

    void OnValidate()
    {
        string e;
        if (!IsValid(out e)) Debug.LogWarning(name + ": " + e, this);
    }
}
