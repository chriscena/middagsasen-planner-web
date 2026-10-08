namespace Middagsasen.Planner.Api.Services.Events
{
    /// <summary>Et anleggskrav på en vaktliste eller mal.</summary>
    public class CompetencyRequirementResponse
    {
        public int CompetencyId { get; internal set; }
        public string CompetencyName { get; internal set; } = null!;
        public int MinimumRequired { get; internal set; }
    }
}
