namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool HasExpiry { get; set; }
        public IEnumerable<int>? ResourceTypeIds { get; set; }
        public IEnumerable<ResourceTypeCompetencyRequest>? ResourceTypeCompetencies { get; set; }
    }
}
