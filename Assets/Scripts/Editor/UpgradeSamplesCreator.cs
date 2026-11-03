#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Menú Tools > Hockey > Crear mejoras de ejemplo (o Assets > Hockey > Crear mejoras de ejemplo).
// Crea en Assets/Data/Upgrades un juego inicial de mejoras y una lista (UpgradePool) con todas.
// No sobrescribe nada: si una mejora ya existe la deja como está (así no perdéis vuestros ajustes).
// ESTE ARCHIVO DEBE ESTAR DENTRO DE UNA CARPETA LLAMADA "Editor".
public static class UpgradeSamplesCreator
{
    const string Folder = "Assets/Data/Upgrades";

    static StatModifier P(StatType s, float percent) => new StatModifier(s, ModifierMode.Percent, percent);
    static StatModifier F(StatType s, float flat) => new StatModifier(s, ModifierMode.Flat, flat);

    [MenuItem("Tools/Hockey/Crear mejoras de ejemplo")]
    [MenuItem("Assets/Hockey/Crear mejoras de ejemplo")]   // también en el menú Assets y con clic derecho en Project
    static void Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "Upgrades");

        List<UpgradeData> all = new List<UpgradeData>();
        const UpgradeRarity C = UpgradeRarity.Comun, R = UpgradeRarity.Rara, E = UpgradeRarity.Epica;

        // ---- Comunes: una estadística, pequeña, acumulables ----
        all.Add(Make("Patines_afilados", "Patins esmolats", "Cada dècima compta.", C, 3, 3,
                     P(StatType.TopSpeed, 6)));
        all.Add(Make("Pulmones_de_acero", "Pulmons d'acer", "Sprint més llarg.", C, 3, 3,
                     P(StatType.SprintDuration, 15)));
        all.Add(Make("Recuperacion", "Recuperació ràpida", "La barra de sprint s'omple abans.", C, 3, 3,
                     P(StatType.SprintRecharge, -15)));
        all.Add(Make("Muneca_fuerte", "Canell fort", "Xuts que piquen més.", C, 3, 3,
                     P(StatType.ShotPower, 8)));
        all.Add(Make("Pase_tenso", "Passada tensa", "Menys temps per interceptar.", C, 3, 3,
                     P(StatType.PassSpeed, 10)));
        all.Add(Make("Manos_rapidas", "Mans ràpides", "Més robatoris amb èxit.", C, 3, 3,
                     F(StatType.StealSkill, 0.08f)));
        all.Add(Make("Bola_pegada", "Bola enganxada a l'stick", "Costa més prendre-te-la.", C, 3, 3,
                     F(StatType.BallControl, 0.08f)));
        all.Add(Make("Portero_agil", "Porter àgil", "Arriba abans als pals.", C, 3, 3,
                     P(StatType.GoalkeeperSpeed, 12)));

        // ---- Raras: dos efectos o uno más fuerte ----
        all.Add(Make("Arranque_explosivo", "Arrencada explosiva", "Primers metres imparables.", R, 2, 5,
                     P(StatType.Acceleration, 15), P(StatType.TopSpeed, 4)));
        all.Add(Make("Stick_largo", "Stick llarg", "Arribes a boles que abans no.", R, 2, 5,
                     P(StatType.StickReach, 12)));
        all.Add(Make("Fintas", "Rei de les fintes", "Regateja més sovint.", R, 2, 5,
                     P(StatType.JukeCooldown, -25)));
        all.Add(Make("Presion", "Pressió asfixiant", "Colpeges amb l'stick sense descans.", R, 2, 5,
                     P(StatType.StealCooldown, -20), F(StatType.StealSkill, 0.05f)));
        all.Add(Make("Muro", "El mur", "El porter cobreix més porteria.", R, 2, 5,
                     P(StatType.GoalkeeperReach, 15)));

        // ---- Épicas: muy fuertes, con un coste ----
        all.Add(Make("Canonazo", "Canonada", "Xuts brutals, però peses més.", E, 1, 8,
                     P(StatType.ShotPower, 20), P(StatType.TopSpeed, -5)));
        all.Add(Make("Motor_turbo", "Motor turbo", "Més ràpid que ningú... durant poca estona.", E, 1, 8,
                     P(StatType.TopSpeed, 14), P(StatType.SprintDuration, -20)));
        all.Add(Make("Mago", "Mag de la bola", "Ningú te la pren, però t'oblides de defensar.", E, 1, 8,
                     F(StatType.BallControl, 0.18f), P(StatType.JukeCooldown, -20), F(StatType.StealSkill, -0.08f)));

        string poolPath = Folder + "/UpgradePool.asset";
        UpgradePool pool = AssetDatabase.LoadAssetAtPath<UpgradePool>(poolPath);
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<UpgradePool>();
            pool.EditorSetup(all);
            AssetDatabase.CreateAsset(pool, poolPath);
            Debug.Log("Mejoras de ejemplo creadas en " + Folder + ". Asigna UpgradePool a tu TournamentConfig.", pool);
        }
        else
        {
            Debug.LogWarning("Ya existe " + poolPath + ": no se ha modificado. Añade a mano las mejoras nuevas al final de la lista.", pool);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = pool;
    }

    static UpgradeData Make(string file, string name, string flavor, UpgradeRarity rarity, int stacks, int power,
                            params StatModifier[] mods)
    {
        string path = Folder + "/Upgrade_" + file + ".asset";
        UpgradeData existing = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        if (existing != null) return existing;

        UpgradeData u = ScriptableObject.CreateInstance<UpgradeData>();
        u.EditorSetup(name, flavor, rarity, stacks, power, mods);
        AssetDatabase.CreateAsset(u, path);
        return u;
    }
}
#endif