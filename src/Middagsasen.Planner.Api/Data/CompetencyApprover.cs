namespace Middagsasen.Planner.Api.Data
{
    public class CompetencyApprover
    {
        public int CompetencyApproverId { get; set; }
        public int CompetencyId { get; set; }
        public int UserId { get; set; }
        public bool Inactive { get; set; }

        public virtual Competency Competency { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
