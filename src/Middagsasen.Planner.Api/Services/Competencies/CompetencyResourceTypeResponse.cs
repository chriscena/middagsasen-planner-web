namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyResourceTypeResponse
    {
        public int ResourceTypeId { get; internal set; }
        public string ResourceTypeName { get; internal set; } = null!;
        public int MinimumRequired { get; internal set; }
    }
}
