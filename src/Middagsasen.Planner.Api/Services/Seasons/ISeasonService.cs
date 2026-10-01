namespace Middagsasen.Planner.Api.Services.Seasons
{
    public interface ISeasonService
    {
        /// <summary>Alle sesonger fra første sesong til og med inneværende, sortert synkende (nyeste først).</summary>
        IEnumerable<SeasonResponse> GetSeasons();
    }
}
