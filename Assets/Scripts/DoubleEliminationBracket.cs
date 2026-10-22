using System;
using System.Collections.Generic;

// Cuadro de doble eliminación (C# puro, sin Unity: se puede probar y serializar con JsonUtility).
//
// ESTRUCTURA para N = 2^k equipos (4, 8, 16...):
//   Cuadro de GANADORES: k rondas (N/2, N/4, ... 1 partidos).
//   Cuadro de PERDEDORES: 2(k-1) rondas que alternan dos tipos:
//     - Ronda "interna" (pares, 0, 2, 4...): los supervivientes del cuadro de perdedores juegan entre sí.
//       (La ronda 0 es especial: la juegan los que han perdido en la 1ª ronda de ganadores.)
//     - Ronda "de caída" (impares 1, 3, 5...): los supervivientes reciben a los que acaban de perder
//       en la ronda j de ganadores (ronda de perdedores 2j-1).
//   GRAN FINAL: campeón de ganadores vs campeón de perdedores. Si gana el de perdedores, los dos tienen
//   una derrota y se juega un partido de desempate ("reset").
//
// Un equipo queda eliminado al sumar 2 derrotas. Cada partido guarda a qué partido van su ganador y su
// perdedor, así que registrar un resultado solo coloca a los dos equipos en su siguiente partido.
[Serializable]
public class DoubleEliminationBracket
{
    public int teamCount;
    public int winnersRounds;
    public int losersRounds;
    public int grandFinalId = -1;
    public int resetMatchId = -1;
    public int championTeam = -1;
    public List<BracketMatch> matches = new List<BracketMatch>();

    public bool IsFinished => championTeam >= 0;

    public static bool IsValidSize(int n) => n >= 4 && (n & (n - 1)) == 0;

    // 'teams' en orden de cabeza de serie (0 = mejor). El 1º se cruza con el último, etc.,
    // y los dos mejores solo pueden encontrarse en la final.
    public static DoubleEliminationBracket Create(IList<int> teams)
    {
        int n = teams.Count;
        if (!IsValidSize(n))
            throw new ArgumentException("El torneo necesita 4, 8, 16... equipos (potencia de 2). Hay " + n);

        int k = 0;
        while ((1 << k) < n) k++;

        DoubleEliminationBracket b = new DoubleEliminationBracket();
        b.teamCount = n;
        b.winnersRounds = k;
        b.losersRounds = 2 * (k - 1);

        // ---- Crear partidos ----
        int[][] wr = new int[k][];
        for (int r = 0; r < k; r++)
        {
            wr[r] = new int[n >> (r + 1)];
            for (int i = 0; i < wr[r].Length; i++)
                wr[r][i] = b.AddMatch(BracketSide.Winners, r, i, 2 * r);
        }

        int[][] lr = new int[b.losersRounds][];
        for (int r = 0; r < b.losersRounds; r++)
        {
            lr[r] = new int[LosersRoundSize(n, r)];
            int stage = r == 0 ? 1 : r + 2;          // siempre después de los partidos de los que depende
            for (int i = 0; i < lr[r].Length; i++)
                lr[r][i] = b.AddMatch(BracketSide.Losers, r, i, stage);
        }

        b.grandFinalId = b.AddMatch(BracketSide.GrandFinal, 0, 0, 2 * k);

        // ---- Rutas del cuadro de ganadores ----
        for (int r = 0; r < k; r++)
        {
            int count = wr[r].Length;
            for (int i = 0; i < count; i++)
            {
                BracketMatch m = b.matches[wr[r][i]];

                if (r < k - 1) { m.winnerToMatch = wr[r + 1][i / 2]; m.winnerToSlot = i % 2; }
                else { m.winnerToMatch = b.grandFinalId; m.winnerToSlot = 0; }

                if (r == 0)
                {
                    m.loserToMatch = lr[0][i / 2];
                    m.loserToSlot = i % 2;
                }
                else
                {
                    // En rondas alternas se invierte el orden para evitar repetir cruces tan pronto
                    int idx = (r % 2 == 1) ? count - 1 - i : i;
                    m.loserToMatch = lr[2 * r - 1][idx];
                    m.loserToSlot = 1;
                }
            }
        }

        // ---- Rutas del cuadro de perdedores ----
        for (int r = 0; r < b.losersRounds; r++)
        {
            for (int i = 0; i < lr[r].Length; i++)
            {
                BracketMatch m = b.matches[lr[r][i]];
                m.loserToMatch = -1;   // segunda derrota: eliminado

                if (r == b.losersRounds - 1) { m.winnerToMatch = b.grandFinalId; m.winnerToSlot = 1; }
                else if (r % 2 == 0) { m.winnerToMatch = lr[r + 1][i]; m.winnerToSlot = 0; }
                else { m.winnerToMatch = lr[r + 1][i / 2]; m.winnerToSlot = i % 2; }
            }
        }

        // ---- Primera ronda con cabezas de serie (1 vs N, 2 vs N-1... repartidos por el cuadro) ----
        int[] order = SeedOrder(n);
        for (int i = 0; i < wr[0].Length; i++)
        {
            BracketMatch m = b.matches[wr[0][i]];
            m.teamA = teams[order[2 * i]];
            m.teamB = teams[order[2 * i + 1]];
        }

        return b;
    }

    static int LosersRoundSize(int n, int r)
    {
        return r % 2 == 0 ? n >> (r / 2 + 2) : n >> ((r + 1) / 2 + 1);
    }

    // Orden estándar de cabezas de serie: 4 -> [0,3,1,2], 8 -> [0,7,3,4,1,6,2,5]
    static int[] SeedOrder(int n)
    {
        List<int> order = new List<int> { 0, 1 };
        while (order.Count < n)
        {
            int size = order.Count * 2;
            List<int> next = new List<int>(size);
            foreach (int s in order) { next.Add(s); next.Add(size - 1 - s); }
            order = next;
        }
        return order.ToArray();
    }

    int AddMatch(BracketSide side, int round, int index, int stage)
    {
        BracketMatch m = new BracketMatch();
        m.id = matches.Count;
        m.side = side;
        m.round = round;
        m.index = index;
        m.order = stage * 1000 + (side == BracketSide.Losers ? 500 : 0) + index;
        matches.Add(m);
        return m.id;
    }

    // ---------- Resultados ----------

    public void RecordResult(int matchId, int scoreA, int scoreB)
    {
        BracketMatch m = Get(matchId);
        if (m == null) throw new ArgumentException("No existe el partido " + matchId);
        if (!m.IsReady) throw new InvalidOperationException("El partido " + matchId + " no está listo o ya se jugó");
        if (scoreA == scoreB) throw new ArgumentException("En eliminatoria no hay empates (gol de oro)");

        m.scoreA = scoreA;
        m.scoreB = scoreB;
        m.winner = scoreA > scoreB ? m.teamA : m.teamB;
        m.loser = scoreA > scoreB ? m.teamB : m.teamA;

        if (m.side == BracketSide.GrandFinal)
        {
            // teamA = campeón de ganadores (sin derrotas). Si pierde, hay desempate.
            if (m.winner == m.teamA) championTeam = m.winner;
            else
            {
                resetMatchId = AddMatch(BracketSide.GrandFinalReset, 0, 0, 2 * winnersRounds + 1);
                BracketMatch reset = matches[resetMatchId];
                reset.teamA = m.teamA;
                reset.teamB = m.teamB;
            }
            return;
        }

        if (m.side == BracketSide.GrandFinalReset)
        {
            championTeam = m.winner;
            return;
        }

        Place(m.winner, m.winnerToMatch, m.winnerToSlot);
        Place(m.loser, m.loserToMatch, m.loserToSlot);
    }

    void Place(int team, int matchId, int slot)
    {
        if (matchId < 0) return;
        BracketMatch target = matches[matchId];
        if (slot == 0) target.teamA = team;
        else target.teamB = team;
    }

    // ---------- Consultas ----------

    public BracketMatch Get(int id) => id >= 0 && id < matches.Count ? matches[id] : null;

    // Siguiente partido a jugar según el calendario (null si no queda ninguno listo)
    public BracketMatch NextReadyMatch()
    {
        BracketMatch best = null;
        foreach (BracketMatch m in matches)
            if (m.IsReady && (best == null || m.order < best.order)) best = m;
        return best;
    }

    public BracketMatch NextMatchFor(int team)
    {
        BracketMatch best = null;
        foreach (BracketMatch m in matches)
            if (!m.IsDone && m.Involves(team) && (best == null || m.order < best.order)) best = m;
        return best;
    }

    public int Losses(int team)
    {
        int l = 0;
        foreach (BracketMatch m in matches) if (m.loser == team) l++;
        return l;
    }

    public bool IsEliminated(int team) => Losses(team) >= 2;

    public List<BracketMatch> Round(BracketSide side, int round)
    {
        List<BracketMatch> list = new List<BracketMatch>();
        foreach (BracketMatch m in matches)
            if (m.side == side && m.round == round) list.Add(m);
        list.Sort((a, c) => a.index.CompareTo(c.index));
        return list;
    }

    public string RoundName(BracketMatch m)
    {
        switch (m.side)
        {
            case BracketSide.Winners:
                if (m.round == winnersRounds - 1) return "Final de ganadores";
                if (m.round == winnersRounds - 2) return "Semifinal de ganadores";
                return "Ganadores · Ronda " + (m.round + 1);
            case BracketSide.Losers:
                if (m.round == losersRounds - 1) return "Final de perdedores";
                return "Perdedores · Ronda " + (m.round + 1);
            case BracketSide.GrandFinal: return "Gran final";
            default: return "Gran final · Desempate";
        }
    }
}
