using System;
using System.Collections.Generic;
using UnityEngine;

// Quién controla un equipo. HumanP2 queda reservado para el PvP (aún no implementado).
public enum ControllerType { CPU, HumanP1, HumanP2 }

// Todo lo que necesita la escena del partido para empezar.
// Es el CONTRATO con la pantalla de selección de equipos: esa pantalla solo tiene que
// rellenar uno de estos y llamar a MatchLauncher.Launch(setup).
[Serializable]
public class MatchSetup
{
    public TeamData homeTeam;
    public TeamData awayTeam;
    public ControllerType homeController = ControllerType.HumanP1;
    public ControllerType awayController = ControllerType.CPU;

    [Tooltip("Si acaba en empate se juega gol de oro")]
    public bool requireWinner = false;

    [Tooltip("Mejoras opcionales de cada equipo (vacío en un 1vs1 normal)")]
    public List<UpgradeData> homeUpgrades = new List<UpgradeData>();
    public List<UpgradeData> awayUpgrades = new List<UpgradeData>();

    public TeamData TeamOf(Team t) => t == Team.Home ? homeTeam : awayTeam;
    public ControllerType ControllerOf(Team t) => t == Team.Home ? homeController : awayController;
    public bool IsPvP => homeController != ControllerType.CPU && awayController != ControllerType.CPU;

    public bool IsValid(out string error)
    {
        error = null;
        if (homeTeam == null || awayTeam == null) error = "Faltan equipos en el MatchSetup";
        else if (homeController == awayController && homeController != ControllerType.CPU)
            error = "Los dos equipos no pueden tener el mismo jugador humano";
        return error == null;
    }

    // Copia independiente (para la revancha, sin compartir listas con el original)
    public MatchSetup Clone()
    {
        MatchSetup c = (MatchSetup)MemberwiseClone();
        c.homeUpgrades = new List<UpgradeData>(homeUpgrades ?? new List<UpgradeData>());
        c.awayUpgrades = new List<UpgradeData>(awayUpgrades ?? new List<UpgradeData>());
        return c;
    }
}
