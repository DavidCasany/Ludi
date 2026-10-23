using System.Collections.Generic;
using UnityEngine;

// Lista de mejoras que pueden salir en el torneo y cómo se sortean. Crear: Create > Hockey > Lista de mejoras.
//
// OFERTA: se eligen 'choices' mejoras distintas sin repetir, con probabilidad según su rareza
// (sorteo ponderado sin reemplazo). No salen las que el equipo ya tiene al máximo (maxStacks).
// La identidad de una mejora en el guardado es su posición en la lista: no reordenéis la lista
// a mitad de un torneo guardado (añadir al final sí se puede).
[CreateAssetMenu(menuName = "Hockey/Lista de mejoras", fileName = "UpgradePool")]
public class UpgradePool : ScriptableObject
{
    [SerializeField] List<UpgradeData> upgrades = new List<UpgradeData>();
    [SerializeField, Range(2, 5)] int choices = 3;

    [Header("Probabilidad por rareza (pesos relativos)")]
    [SerializeField, Min(0f)] float commonWeight = 60f;
    [SerializeField, Min(0f)] float rareWeight = 30f;
    [SerializeField, Min(0f)] float epicWeight = 10f;

    public int Count => upgrades.Count;
    public int Choices => choices;
    public UpgradeData Get(int id) => id >= 0 && id < upgrades.Count ? upgrades[id] : null;

    float Weight(UpgradeData u)
    {
        switch (u.Rarity)
        {
            case UpgradeRarity.Epica: return epicWeight;
            case UpgradeRarity.Rara: return rareWeight;
            default: return commonWeight;
        }
    }

    static int CountOf(IList<int> owned, int id)
    {
        int c = 0;
        for (int i = 0; i < owned.Count; i++) if (owned[i] == id) c++;
        return c;
    }

    // Devuelve los ids de la oferta (puede tener menos de 'choices' si quedan pocas disponibles)
    public List<int> GenerateOffer(IList<int> owned, System.Random rng)
    {
        List<int> candidates = new List<int>();
        List<float> weights = new List<float>();
        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData u = upgrades[i];
            if (u == null || CountOf(owned, i) >= u.MaxStacks) continue;
            float w = Weight(u);
            if (w <= 0f) continue;
            candidates.Add(i);
            weights.Add(w);
        }

        List<int> offer = new List<int>();
        while (offer.Count < choices && candidates.Count > 0)
        {
            float total = 0f;
            foreach (float w in weights) total += w;

            double roll = rng.NextDouble() * total;
            int pick = candidates.Count - 1;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= weights[i];
                if (roll <= 0) { pick = i; break; }
            }

            offer.Add(candidates[pick]);
            candidates.RemoveAt(pick);
            weights.RemoveAt(pick);
        }
        return offer;
    }

    // Elección de la IA: favorece las mejoras más potentes, pero con algo de azar (no siempre la "mejor")
    public int AiChoose(IList<int> offer, System.Random rng)
    {
        float total = 0f;
        foreach (int id in offer) total += 1f + upgrades[id].SimPower;

        double roll = rng.NextDouble() * total;
        foreach (int id in offer)
        {
            roll -= 1f + upgrades[id].SimPower;
            if (roll <= 0) return id;
        }
        return offer[offer.Count - 1];
    }

#if UNITY_EDITOR
    public void EditorSetup(List<UpgradeData> list) { upgrades = list; }
#endif
}
