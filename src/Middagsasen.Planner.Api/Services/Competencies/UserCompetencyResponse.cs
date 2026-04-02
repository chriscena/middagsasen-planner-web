namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class UserCompetencyResponse
    {
        public int Id { get; internal set; }
        public int UserId { get; internal set; }
        public string UserFullName { get; internal set; } = null!;
        public int CompetencyId { get; internal set; }
        public string CompetencyName { get; internal set; } = null!;
        public bool Approved { get; internal set; }
        public DateTime? ApprovedDate { get; internal set; }
        public int? ApprovedById { get; internal set; }
        public string? ApprovedByName { get; internal set; }
        public DateTime? ExpiryDate { get; internal set; }
        public bool IsExpired { get; internal set; }
        public DateTime Created { get; internal set; }
    }
}
