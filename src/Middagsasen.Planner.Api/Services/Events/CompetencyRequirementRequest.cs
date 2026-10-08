namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>Et anleggskrav på en vaktliste eller mal.</summary>
    public class CompetencyRequirementRequest
    {
        public int CompetencyId { get; set; }
        /// <summary>Minst så mange på vakt i anlegget skal ha kompetansen på hvert tidspunkt i åpningstiden. Må være minst 1.</summary>
        public int MinimumRequired { get; set; }
    }
}
