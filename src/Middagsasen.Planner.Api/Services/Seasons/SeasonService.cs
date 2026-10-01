using Middagsasen.Planner.Api.Core;

namespace Middagsasen.Planner.Api.Services.Seasons
{
    public class SeasonService : ISeasonService
    {
        public SeasonService(TimeProvider timeProvider)
        {
            TimeProvider = timeProvider;
        }

        public TimeProvider TimeProvider { get; }

        public IEnumerable<SeasonResponse> GetSeasons()
        {
            var currentStartYear = TimeProvider.GetUtcNow().UtcDateTime.GetSeasonStartYear();
            var firstStartYear = Math.Min(DateTimeExtensions.FirstSeasonStartYear, currentStartYear);

            var seasons = new List<SeasonResponse>();
            for (var year = currentStartYear; year >= firstStartYear; year--)
            {
                seasons.Add(new SeasonResponse
                {
                    StartYear = year,
                    Label = DateTimeExtensions.ToSeasonLabel(year),
                    IsCurrent = year == currentStartYear,
                });
            }
            return seasons;
        }
    }
}
