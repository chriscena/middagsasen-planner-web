namespace Middagsasen.Planner.Api.Data
{
    /// <summary>Anleggskrav på en mal. Kopieres til vaktlisten når den opprettes fra malen.</summary>
    public class EventTemplateCompetencyRequirement
    {
        public int EventTemplateCompetencyRequirementId { get; set; }
        public int EventTemplateId { get; set; }
        public int CompetencyId { get; set; }
        public int MinimumRequired { get; set; }

        public virtual EventTemplate EventTemplate { get; set; } = null!;
        public virtual Competency Competency { get; set; } = null!;
    }
}
