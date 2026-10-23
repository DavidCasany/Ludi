using System;
using UnityEngine;

// Estadísticas del equipo que puede tocar una mejora (se aplican a todos los jugadores de campo o al portero)
public enum StatType
{
    TopSpeed,        // Skater.maxSpeed
    Acceleration,    // Skater.acceleration
    SprintDuration,  // Skater.sprintDuration (más = mejor)
    SprintRecharge,  // Skater.sprintRecharge (segundos: menos = mejor)
    ShotPower,       // Skater.shootForce
    PassSpeed,       // Skater.passSpeedPerMeter / Min / Max
    StealSkill,      // Skater.stealRating (0-1)
    BallControl,     // Skater.ballControl (0-1)
    StickReach,      // Skater.stickReach
    StealCooldown,   // Skater.stealCooldown (menos = mejor)
    JukeCooldown,    // Skater.jukeCooldown (menos = mejor)
    GoalkeeperSpeed, // Goalkeeper.speed
    GoalkeeperReach  // Goalkeeper.reach
}

public enum ModifierMode
{
    Percent,   // value = porcentaje (10 = +10 %)
    Flat       // value se suma tal cual (para los atributos 0-1: 0.1 = +10 puntos)
}

[Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierMode mode;
    public float value;

    public StatModifier(StatType stat, ModifierMode mode, float value)
    {
        this.stat = stat;
        this.mode = mode;
        this.value = value;
    }

    // ¿Es bueno para el equipo? (en los tiempos de recarga, bajar es mejorar)
    public bool IsBeneficial => LowerIsBetter(stat) ? value < 0f : value > 0f;

    public static bool LowerIsBetter(StatType s) =>
        s == StatType.SprintRecharge || s == StatType.StealCooldown || s == StatType.JukeCooldown;

    public string Describe()
    {
        string sign = value >= 0f ? "+" : "−";
        float a = Mathf.Abs(value);

        if (mode == ModifierMode.Percent) return $"{sign}{a:0.#} % {Label(stat)}";
        if (stat == StatType.StealSkill || stat == StatType.BallControl) return $"{sign}{a * 100f:0} {Label(stat)}";
        return $"{sign}{a:0.##} {Label(stat)}";
    }

    public static string Label(StatType s)
    {
        switch (s)
        {
            case StatType.TopSpeed: return "velocidad";
            case StatType.Acceleration: return "aceleración";
            case StatType.SprintDuration: return "duración del sprint";
            case StatType.SprintRecharge: return "tiempo de recarga del sprint";
            case StatType.ShotPower: return "potencia de tiro";
            case StatType.PassSpeed: return "velocidad de pase";
            case StatType.StealSkill: return "robo";
            case StatType.BallControl: return "control de bola";
            case StatType.StickReach: return "alcance del stick";
            case StatType.StealCooldown: return "recarga del stick";
            case StatType.JukeCooldown: return "recarga del regate";
            case StatType.GoalkeeperSpeed: return "velocidad del portero";
            default: return "recorrido del portero";
        }
    }
}
