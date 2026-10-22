using System;

public enum BracketSide { Winners, Losers, GrandFinal, GrandFinalReset }

// Un partido del cuadro. Los equipos son índices en la lista de equipos del torneo (-1 = por decidir).
// Cada partido sabe a dónde van su ganador y su perdedor (rutas), así que registrar un resultado
// solo tiene que "empujar" a los dos equipos a su siguiente partido.
[Serializable]
public class BracketMatch
{
    public int id;
    public BracketSide side;
    public int round;          // ronda dentro de su cuadro (0 = primera)
    public int index;          // posición dentro de la ronda
    public int order;          // orden de calendario (menor = se juega antes)

    public int teamA = -1;
    public int teamB = -1;
    public int scoreA;
    public int scoreB;
    public int winner = -1;
    public int loser = -1;

    // Rutas (-1 = no va a ningún partido: el ganador es campeón / el perdedor queda eliminado)
    public int winnerToMatch = -1;
    public int winnerToSlot;
    public int loserToMatch = -1;
    public int loserToSlot;

    public bool IsReady => teamA >= 0 && teamB >= 0 && winner < 0;
    public bool IsDone => winner >= 0;
    public bool Involves(int team) => team >= 0 && (teamA == team || teamB == team);
    public int OpponentOf(int team) => teamA == team ? teamB : (teamB == team ? teamA : -1);
}
