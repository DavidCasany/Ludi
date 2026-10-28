// Resultado de un partido jugado. Lo genera MatchBootstrapper al terminar
// y queda disponible en MatchLauncher.LastResult para la pantalla de resultados.
public class MatchResult
{
    public readonly MatchSetup setup;
    public readonly int homeGoals;
    public readonly int awayGoals;

    public MatchResult(MatchSetup setup, int homeGoals, int awayGoals)
    {
        this.setup = setup;
        this.homeGoals = homeGoals;
        this.awayGoals = awayGoals;
    }

    public bool IsDraw => homeGoals == awayGoals;
    public Team? Winner => IsDraw ? (Team?)null : homeGoals > awayGoals ? Team.Home : Team.Away;
    public TeamData WinnerData => Winner.HasValue ? setup.TeamOf(Winner.Value) : null;
}
