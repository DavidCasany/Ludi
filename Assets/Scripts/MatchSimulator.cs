using System;

// Resultado rápido de un partido que no juega el humano.
// Los goles de cada equipo siguen una Poisson cuya media depende de la fuerza relativa:
//   mediaA = goalsPerTeam * 2 * ratingA / (ratingA + ratingB)
// Con empate se decide por gol de oro: gana A con probabilidad ratingA / (ratingA + ratingB).
public static class MatchSimulator
{
    public static void Simulate(int ratingA, int ratingB, float goalsPerTeam, Random rng,
                                out int goalsA, out int goalsB)
    {
        float share = ratingA / (float)Math.Max(1, ratingA + ratingB);
        goalsA = Poisson(goalsPerTeam * 2f * share, rng);
        goalsB = Poisson(goalsPerTeam * 2f * (1f - share), rng);

        if (goalsA == goalsB)
        {
            if (rng.NextDouble() < share) goalsA++;
            else goalsB++;
        }
    }

    // Algoritmo de Knuth (válido para medias pequeñas como las de un partido)
    static int Poisson(float mean, Random rng)
    {
        double limit = Math.Exp(-mean), p = 1.0;
        int k = 0;
        do { k++; p *= rng.NextDouble(); } while (p > limit);
        return k - 1;
    }
}
