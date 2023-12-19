namespace Middagsasen.Planner.Api.Data
{
    public class ResourceTypeTrainer
    {
        public int ResourceTypeTrainerId { get; set; }
        public int ResourceTypeId { get; set; }
        public int UserId { get; set; }
        public bool Inactive { get; set; }

        public virtual ResourceType ResourceType { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
