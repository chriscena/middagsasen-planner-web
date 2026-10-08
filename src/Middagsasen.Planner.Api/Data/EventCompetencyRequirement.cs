namespace Middagsasen.Planner.Api.Data
{
    /// <summary>
    /// Anleggskrav på en vaktliste: minst <see cref="MinimumRequired"/> av de som er på vakt i anlegget skal ha
    /// kompetansen på hvert tidspunkt i åpningstiden, uansett vakttype.
    /// </summary>
    public class EventCompetencyRequirement
    {
        public int EventCompetencyRequirementId { get; set; }
        public int EventId { get; set; }
        public int CompetencyId { get; set; }
        public int MinimumRequired { get; set; }

        public virtual Event Event { get; set; } = null!;
        public virtual Competency Competency { get; set; } = null!;
    }
}
