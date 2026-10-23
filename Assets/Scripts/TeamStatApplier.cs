using System.Collections.Generic;
using UnityEngine;

// Aplica las mejoras de un equipo a sus jugadores al empezar el partido.
// Para cada estadística: valor = (base + suma de planos) * (1 + suma de porcentajes).
// Los porcentajes se SUMAN (dos +10 % = +20 %, no +21 %), así el crecimiento es fácil de equilibrar.
// Como la escena del partido se carga de nuevo cada vez, los valores base son siempre los del prefab:
// no hay riesgo de aplicar las mejoras dos veces.
public static class TeamStatApplier
{
    static readonly int StatCount = System.Enum.GetValues(typeof(StatType)).Length;

    public static void Apply(TeamManager tm, IReadOnlyList<UpgradeData> upgrades)
    {
        if (tm == null || upgrades == null || upgrades.Count == 0) return;

        float[] flat = new float[StatCount];
        float[] pct = new float[StatCount];

        foreach (UpgradeData u in upgrades)
        {
            if (u == null) continue;
            foreach (StatModifier m in u.Modifiers)
            {
                if (m.mode == ModifierMode.Percent) pct[(int)m.stat] += m.value / 100f;
                else flat[(int)m.stat] += m.value;
            }
        }

        float Mod(float baseValue, StatType s)
        {
            int i = (int)s;
            return (baseValue + flat[i]) * Mathf.Max(0.2f, 1f + pct[i]);   // nunca baja de un 20 %
        }

        foreach (Skater s in tm.skaters)
        {
            s.maxSpeed = Mod(s.maxSpeed, StatType.TopSpeed);
            s.acceleration = Mod(s.acceleration, StatType.Acceleration);
            s.sprintDuration = Mathf.Max(0.3f, Mod(s.sprintDuration, StatType.SprintDuration));
            s.sprintRecharge = Mathf.Max(0.3f, Mod(s.sprintRecharge, StatType.SprintRecharge));
            s.shootForce = Mod(s.shootForce, StatType.ShotPower);

            s.passSpeedPerMeter = Mod(s.passSpeedPerMeter, StatType.PassSpeed);
            s.passSpeedMin = Mod(s.passSpeedMin, StatType.PassSpeed);
            s.passSpeedMax = Mod(s.passSpeedMax, StatType.PassSpeed);

            s.stealRating = Mathf.Clamp01(Mod(s.stealRating, StatType.StealSkill));
            s.ballControl = Mathf.Clamp01(Mod(s.ballControl, StatType.BallControl));
            s.stickReach = Mod(s.stickReach, StatType.StickReach);
            s.stealCooldown = Mathf.Max(0.1f, Mod(s.stealCooldown, StatType.StealCooldown));
            s.jukeCooldown = Mathf.Max(0.2f, Mod(s.jukeCooldown, StatType.JukeCooldown));
        }

        if (tm.goalkeeper != null)
        {
            tm.goalkeeper.speed = Mod(tm.goalkeeper.speed, StatType.GoalkeeperSpeed);
            tm.goalkeeper.reach = Mod(tm.goalkeeper.reach, StatType.GoalkeeperReach);
        }
    }
}
