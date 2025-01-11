namespace Middagsasen.Planner.Api.Data
{
    public class ResourceTypeTraining
    {
        public int ResourceTypeTrainingId { get; set; }
        public int UserId { get; set; }
        public int ResourceTypeId { get; set; }
        public bool? TrainingComplete { get; set; }
        public DateTime? Confirmed { get; set; }
        public int? ConfirmedBy { get; set; }

        public virtual ResourceType ResourceType { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual User ConfirmedByUser { get; set; } = null!;
    }
}
