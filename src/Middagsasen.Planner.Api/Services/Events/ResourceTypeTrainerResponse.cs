namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTypeTrainerResponse
    {
        public int Id { get; internal set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string PhoneNo { get; set; } = null!;
    }
}