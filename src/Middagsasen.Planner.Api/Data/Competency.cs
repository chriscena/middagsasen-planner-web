namespace Middagsasen.Planner.Api.Data
{
    public class Competency
    {
        public int CompetencyId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool HasExpiry { get; set; }
        public bool Inactive { get; set; }

        public virtual ICollection<UserCompetency> UserCompetencies { get; set; } = new HashSet<UserCompetency>();
        public virtual ICollection<CompetencyApprover> CompetencyApprovers { get; set; } = new HashSet<CompetencyApprover>();
        public virtual ICollection<ResourceTypeCompetency> ResourceTypeCompetencies { get; set; } = new HashSet<ResourceTypeCompetency>();
    }
}
