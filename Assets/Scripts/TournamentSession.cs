using System;
using System.Collections.Generic;
using UnityEngine;

// Estado del torneo en curso. Es estático para sobrevivir a los cambios de escena
// (hub <-> partido) sin objetos DontDestroyOnLoad. Se guarda en PlayerPrefs tras cada cambio.
//
// Los equipos se identifican por su índice en TournamentConfig.Teams.
//
// MEJORAS (roguelite): cada equipo tiene derecho a  startingPicks + jornada  mejoras antes de jugar un
// partido de esa jornada. Como la jornada solo avanza con el calendario, todos los equipos vivos tienen
// las mismas mejoras al llegar a la misma jornada, vayan por el camino que vayan. Quien avanza sin perder
// se salta jornadas (espera mientras el cuadro de perdedores juega) y las recupera de golpe: doble elección.
// Ej. con 8 equipos, los dos finalistas llegan a la gran final con startingPicks + 6 mejoras.
public static class TournamentSession
{
    const string SaveKey = "hockey_tournament_save";

    [Serializable]
    public class TeamProgress
    {
        public List<int> upgrades = new List<int>();   // ids en el UpgradePool
        public int picksTaken;                         // elecciones hechas (incluye las que se quedaron sin oferta)
    }

    [Serializable]
    class SaveData
    {
        public string configName;
        public int playerTeam;
        public int rngSeed;
        public int simulatedCount;
        public DoubleEliminationBracket bracket;
        public List<TeamProgress> progress;
        public List<int> playerOffer;
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
    static List<TeamProgress> progress = new List<TeamProgress>();
    static List<int> playerOffer = new List<int>();   // se guarda para que recargar no cambie la oferta

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Config = null;
        Bracket = null;
        PlayerTeam = -1;
        rng = null;
        progress = new List<TeamProgress>();
        playerOffer = new List<int>();
        LastSimulated.Clear();
    }

    // ---------- Inicio ----------

    public static bool StartNew(TournamentConfig config)
    {
        string error="";
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
        ResetProgress();

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

    static void ResetProgress()
    {
        progress = new List<TeamProgress>();
        for (int i = 0; i < Config.Teams.Count; i++) progress.Add(new TeamProgress());
        playerOffer = new List<int>();
    }

    // ---------- Avance del calendario ----------

    // Simula en orden los partidos IA vs IA hasta que el siguiente sea del jugador.
    // Antes de cada partido, los equipos de la IA hacen las elecciones de mejora que les tocan.
    // Devuelve el partido del jugador, o null si está eliminado (entonces se simula todo hasta el final).
    public static BracketMatch AdvanceToPlayerMatch()
    {
        LastSimulated.Clear();
        if (!IsActive) return null;

        while (!Bracket.IsFinished)
        {
            BracketMatch next = Bracket.NextReadyMatch();
            if (next == null) break;

            CatchUpAI(next.teamA, next.Stage);
            CatchUpAI(next.teamB, next.Stage);

            if (next.Involves(PlayerTeam))
            {
                Save();
                return next;
            }

            int a, b;
            MatchSimulator.Simulate(EffectiveRating(next.teamA), EffectiveRating(next.teamB),
                                    Config.GoalsPerTeam, rng, out a, out b);
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

    // ---------- Mejoras ----------

    static UpgradePool Pool => Config != null ? Config.UpgradePool : null;
    public static bool UpgradesEnabled => Pool != null && Pool.Count > 0;

    // Elecciones a las que tiene derecho un equipo antes de un partido de la jornada 'stage'
    public static int PicksDueBefore(int stage) => Config.StartingPicks + stage;

    public static UpgradeData Upgrade(int id) => Pool != null ? Pool.Get(id) : null;

    public static IReadOnlyList<int> UpgradeIdsOf(int team) =>
        team >= 0 && team < progress.Count ? progress[team].upgrades : (IReadOnlyList<int>)Array.Empty<int>();

    public static List<UpgradeData> UpgradesOf(int team)
    {
        List<UpgradeData> list = new List<UpgradeData>();
        foreach (int id in UpgradeIdsOf(team))
        {
            UpgradeData u = Upgrade(id);
            if (u != null) list.Add(u);
        }
        return list;
    }

    // Fuerza para la simulación: rating base + potencia de sus mejoras
    public static int EffectiveRating(int team)
    {
        int r = Team(team).Rating;
        foreach (int id in UpgradeIdsOf(team))
        {
            UpgradeData u = Upgrade(id);
            if (u != null) r += u.SimPower;
        }
        return r;
    }

    // La IA hace todas las elecciones que le faltan hasta la jornada indicada (el jugador elige a mano)
    static void CatchUpAI(int team, int stage)
    {
        if (!UpgradesEnabled || team < 0 || team == PlayerTeam) return;

        TeamProgress p = progress[team];
        int due = PicksDueBefore(stage);
        while (p.picksTaken < due)
        {
            List<int> offer = Pool.GenerateOffer(p.upgrades, rng);
            if (offer.Count > 0) p.upgrades.Add(Pool.AiChoose(offer, rng));
            p.picksTaken++;   // sin oferta (todo al máximo) la elección se pierde, igual que para el jugador
        }
    }

    // Elecciones que le quedan al jugador antes de su próximo partido
    public static int PlayerPicksPending
    {
        get
        {
            if (!UpgradesEnabled) return 0;
            BracketMatch m = PendingPlayerMatch();
            if (m == null) return 0;
            return Mathf.Max(0, PicksDueBefore(m.Stage) - progress[PlayerTeam].picksTaken);
        }
    }

    // Total de elecciones de esta ronda de elecciones (para mostrar "Mejora 1 de 2")
    public static int PlayerPicksThisRound { get; private set; }

    // La oferta actual de 3 mejoras para el jugador (ids). Vacía si no le toca elegir.
    public static IReadOnlyList<int> PlayerOffer
    {
        get
        {
            EnsurePlayerOffer();
            return playerOffer;
        }
    }

    static void EnsurePlayerOffer()
    {
        if (playerOffer.Count > 0 || !IsActive || !UpgradesEnabled) return;

        TeamProgress p = progress[PlayerTeam];
        while (PlayerPicksPending > 0)
        {
            List<int> offer = Pool.GenerateOffer(p.upgrades, rng);
            if (offer.Count > 0)
            {
                playerOffer = offer;
                Save();
                return;
            }
            p.picksTaken++;   // no queda ninguna mejora disponible: se salta la elección
        }
    }

    public static void ChoosePlayerUpgrade(int offerIndex)
    {
        if (offerIndex < 0 || offerIndex >= playerOffer.Count || PlayerPicksPending <= 0) return;

        TeamProgress p = progress[PlayerTeam];
        p.upgrades.Add(playerOffer[offerIndex]);
        p.picksTaken++;
        playerOffer.Clear();
        Save();
    }

    // Lo llama el hub al entrar: fija cuántas elecciones hay en esta tanda
    public static void BeginPickRound() => PlayerPicksThisRound = PlayerPicksPending;

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

        // Partidas guardadas antes de existir las mejoras: se empieza el progreso de cero
        if (d.progress != null && d.progress.Count == config.Teams.Count)
        {
            progress = d.progress;
            playerOffer = d.playerOffer ?? new List<int>();
        }
        else ResetProgress();

        // Semilla derivada para que, al continuar, no se repitan los mismos resultados aleatorios
        rng = new System.Random(rngSeed + simulatedCount + 7919 * TotalPicks());
        LastSimulated.Clear();
        return true;
    }

    static int TotalPicks()
    {
        int t = 0;
        foreach (TeamProgress p in progress) t += p.picksTaken;
        return t;
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
        d.progress = progress;
        d.playerOffer = playerOffer;

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
