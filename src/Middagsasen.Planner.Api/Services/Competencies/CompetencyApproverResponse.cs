namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class CompetencyApproverResponse
    {
        public int Id { get; internal set; }
        public int UserId { get; internal set; }
        public string FullName { get; internal set; } = null!;
    }
}
