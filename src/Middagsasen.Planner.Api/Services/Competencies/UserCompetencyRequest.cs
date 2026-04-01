namespace Middagsasen.Planner.Api.Services.Competencies
{
    public class UserCompetencyRequest
    {
        public int UserId { get; set; }
        public int CompetencyId { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
