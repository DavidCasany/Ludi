using System;
using System.Collections.Generic;
using UnityEngine;

// Estado del torneo en curso. Es estático para sobrevivir a los cambios de escena
// (hub <-> partido) sin objetos DontDestroyOnLoad. Se guarda en PlayerPrefs tras cada resultado.
//
// Los equipos se identifican por su índice en TournamentConfig.Teams.
public static class TournamentSession
{
    const string SaveKey = "hockey_tournament_save";

    [Serializable]
    class SaveData
    {
        public string configName;
        public int playerTeam;
        public int rngSeed;
        public int simulatedCount;
        public DoubleEliminationBracket bracket;
    }

    public static TournamentConfig Config { get; private set; }
    public static DoubleEliminationBracket Bracket { get; private set; }
    public static int PlayerTeam { get; private set; } = -1;

    public static bool IsActive => Config != null && Bracket != null;
    public static bool PlayerEliminated => IsActive && Bracket.IsEliminated(PlayerTeam);
    public static bool IsChampion => IsActive && Bracket.championTeam == PlayerTeam;

    // Resultados simulados desde la última vez que se llamó a AdvanceToPlayerMatch (para mostrarlos en el hub)
    public static readonly List<BracketMatch> LastSimulated = new List<BracketMatch>();

    static System.Random rng;
    static int rngSeed;
    static int simulatedCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Config = null;
        Bracket = null;
        PlayerTeam = -1;
        rng = null;
        LastSimulated.Clear();
    }

    // ---------- Inicio ----------

    public static bool StartNew(TournamentConfig config)
    {
        string error = "";

        if (config == null || !config.IsValid(out error))
        {
            Debug.LogError("Torneo no válido: " + (config == null ? "sin configuración" : error));
            return false;
        }

        Config = config;
        PlayerTeam = IndexOf(config.PlayerTeam);
        rngSeed = Environment.TickCount;
        rng = new System.Random(rngSeed);
        simulatedCount = 0;
        LastSimulated.Clear();

        List<int> seeds = new List<int>();
        for (int i = 0; i < config.Teams.Count; i++) seeds.Add(i);
        if (config.ShuffleSeeds)
        {
            for (int i = seeds.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (seeds[i], seeds[j]) = (seeds[j], seeds[i]);
            }
        }

        Bracket = DoubleEliminationBracket.Create(seeds);
        Save();
        return true;
    }

    // ---------- Avance del calendario ----------

    // Simula en orden los partidos IA vs IA hasta que el siguiente sea del jugador.
    // Devuelve ese partido, o null si el jugador está eliminado (entonces se simula todo hasta el final).
    public static BracketMatch AdvanceToPlayerMatch()
    {
        LastSimulated.Clear();
        if (!IsActive) return null;

        while (!Bracket.IsFinished)
        {
            BracketMatch next = Bracket.NextReadyMatch();
            if (next == null) break;
            if (next.Involves(PlayerTeam)) return next;

            int a, b;
            MatchSimulator.Simulate(Team(next.teamA).Rating, Team(next.teamB).Rating, Config.GoalsPerTeam, rng, out a, out b);
            Bracket.RecordResult(next.id, a, b);
            simulatedCount++;
            LastSimulated.Add(next);
        }

        Save();
        return null;
    }

    public static BracketMatch PendingPlayerMatch()
    {
        if (!IsActive) return null;
        BracketMatch next = Bracket.NextReadyMatch();
        return next != null && next.Involves(PlayerTeam) ? next : null;
    }

    // Lo llama el partido al terminar
    public static void ReportPlayerMatch(int playerGoals, int rivalGoals)
    {
        BracketMatch m = PendingPlayerMatch();
        if (m == null)
        {
            Debug.LogWarning("TournamentSession: no hay partido del jugador pendiente");
            return;
        }

        bool playerIsA = m.teamA == PlayerTeam;
        Bracket.RecordResult(m.id, playerIsA ? playerGoals : rivalGoals, playerIsA ? rivalGoals : playerGoals);
        Save();
    }

    // ---------- Consultas ----------

    public static TeamData Team(int index) => Config.Teams[index];

    public static TeamData RivalIn(BracketMatch m) => Team(m.OpponentOf(PlayerTeam));

    static int IndexOf(TeamData t)
    {
        for (int i = 0; i < Config.Teams.Count; i++) if (Config.Teams[i] == t) return i;
        return -1;
    }

    // ---------- Guardado ----------

    public static bool HasSave(TournamentConfig config)
    {
        SaveData d = LoadData();
        return d != null && config != null && d.configName == config.name && d.bracket != null &&
               d.bracket.teamCount == config.Teams.Count && !d.bracket.IsFinished;
    }

    public static bool TryResume(TournamentConfig config)
    {
        if (!HasSave(config)) return false;
        SaveData d = LoadData();

        Config = config;
        Bracket = d.bracket;
        PlayerTeam = d.playerTeam;
        rngSeed = d.rngSeed;
        simulatedCount = d.simulatedCount;

        // Semilla derivada para que, al continuar, no se repitan los mismos resultados aleatorios
        rng = new System.Random(rngSeed + simulatedCount);
        LastSimulated.Clear();
        return true;
    }

    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    static void Save()
    {
        if (!IsActive) return;

        SaveData d = new SaveData();
        d.configName = Config.name;
        d.playerTeam = PlayerTeam;
        d.rngSeed = rngSeed;
        d.simulatedCount = simulatedCount;
        d.bracket = Bracket;

        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    static SaveData LoadData()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return null;
        try { return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); }
        catch (Exception) { return null; }
    }
}
