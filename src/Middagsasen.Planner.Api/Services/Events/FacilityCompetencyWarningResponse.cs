namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>
    /// Brudd på et anleggskrav: i tidsrommet [<see cref="StartTime"/>, <see cref="EndTime"/>) har bare
    /// <see cref="CurrentCount"/> av de som er på vakt i anlegget kompetansen. Bare en advarsel; blokkerer ikke påmelding.
    /// </summary>
    public class FacilityCompetencyWarningResponse
    {
        public string CompetencyName { get; internal set; } = null!;
        public int MinimumRequired { get; internal set; }
        /// <summary>Antall ulike brukere på vakt med gyldig kompetanse i hele tidsrommet.</summary>
        public int CurrentCount { get; internal set; }
        /// <summary>Start på bruddet, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string StartTime { get; internal set; } = null!;
        /// <summary>Slutt på bruddet (ikke inkludert), norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string EndTime { get; internal set; } = null!;
    }
}
