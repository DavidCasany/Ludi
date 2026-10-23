using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum UpgradeRarity { Comun, Rara, Epica }

// Una mejora de equipo que se elige entre partidos. Crear: Create > Hockey > Mejora.
// Cada mejora tiene uno o varios modificadores de estadística (pueden ser negativos: mejoras con coste).
[CreateAssetMenu(menuName = "Hockey/Mejora", fileName = "Upgrade_")]
public class UpgradeData : ScriptableObject
{
    [SerializeField] string displayName = "Mejora";
    [SerializeField, TextArea(2, 4)] string flavorText = "";
    [SerializeField] Sprite icon;
    [SerializeField] UpgradeRarity rarity = UpgradeRarity.Comun;

    [Tooltip("Cuántas veces la puede tener un mismo equipo")]
    [SerializeField, Min(1)] int maxStacks = 2;

    [Tooltip("Puntos de rating que suma en los partidos simulados (IA vs IA). Común ≈ 3, rara ≈ 5, épica ≈ 8")]
    [SerializeField, Min(0)] int simPower = 3;

    [SerializeField] List<StatModifier> modifiers = new List<StatModifier>();

    public string DisplayName => displayName;
    public string FlavorText => flavorText;
    public Sprite Icon => icon;
    public UpgradeRarity Rarity => rarity;
    public int MaxStacks => maxStacks;
    public int SimPower => simPower;
    public IReadOnlyList<StatModifier> Modifiers => modifiers;

    // Texto con los efectos, una línea por modificador (para la carta)
    public string EffectsText()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(modifiers[i].Describe());
        }
        return sb.ToString();
    }

    public static string RarityName(UpgradeRarity r) =>
        r == UpgradeRarity.Epica ? "ÉPICA" : r == UpgradeRarity.Rara ? "RARA" : "COMÚN";

#if UNITY_EDITOR
    // Solo para el script de editor que crea las mejoras de ejemplo
    public void EditorSetup(string name, string flavor, UpgradeRarity rar, int stacks, int power, params StatModifier[] mods)
    {
        displayName = name;
        flavorText = flavor;
        rarity = rar;
        maxStacks = stacks;
        simPower = power;
        modifiers = new List<StatModifier>(mods);
    }
#endif
}
