namespace Middagsasen.Planner.Api.Services.Events
{
    public class ShiftUserResponse
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FullName { get; set; }
        public IEnumerable<TrainingResponse> Trainings { get; set; } = new List<TrainingResponse>();
    }
}