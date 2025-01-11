namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class TrainingRequest
    {
        public int Id { get; set; }
        public int ResourceTypeId { get; set; }
        public int UserId { get; set; }
        public DateTime StartTime { get; set; }
        public bool? TrainingCompleted { get; set; }
        public int ConfirmedBy { get; set; }
    }
}