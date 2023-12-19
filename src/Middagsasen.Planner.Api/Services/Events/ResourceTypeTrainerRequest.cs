namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTypeTrainerRequest
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public bool IsDeleted { get; set; }
    }
}