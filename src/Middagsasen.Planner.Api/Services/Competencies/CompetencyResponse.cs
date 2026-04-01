namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyResponse
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = null!;
        public string? Description { get; internal set; }
        public bool HasExpiry { get; internal set; }
        public bool Inactive { get; internal set; }
        public IEnumerable<CompetencyResourceTypeResponse> ResourceTypes { get; internal set; } = new List<CompetencyResourceTypeResponse>();
        public IEnumerable<CompetencyApproverResponse> Approvers { get; internal set; } = new List<CompetencyApproverResponse>();
    }
}
