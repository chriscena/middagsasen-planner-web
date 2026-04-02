namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class ResourceTypeCompetencyResponse
    {
        public int CompetencyId { get; set; }
        public string CompetencyName { get; set; } = null!;
        public int MinimumRequired { get; set; }
    }
}
