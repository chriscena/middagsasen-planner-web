namespace Middagsasen.Planner.Api.Data
{
    public class UserCompetency
    {
        public int UserCompetencyId { get; set; }
        public int UserId { get; set; }
        public int CompetencyId { get; set; }
        public bool Approved { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime Created { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual Competency Competency { get; set; } = null!;
        public virtual User? ApprovedByUser { get; set; }
    }
}
