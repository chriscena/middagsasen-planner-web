namespace Middagsasen.Planner.Api.Data
{
    public class ResourceTypeCompetency
    {
        public int ResourceTypeCompetencyId { get; set; }
        public int ResourceTypeId { get; set; }
        public int CompetencyId { get; set; }
        public int MinimumRequired { get; set; }

        public virtual ResourceType ResourceType { get; set; } = null!;
        public virtual Competency Competency { get; set; } = null!;
    }
}
