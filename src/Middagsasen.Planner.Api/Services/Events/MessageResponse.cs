namespace Middagsasen.Planner.Api.Services.Events
{
    public class MessageResponse
    {
        public int Id { get; set; }
        public int EventResourceId { get; set; }
        public ShiftUserResponse CreatedBy { get; set; } = null!;
        public string Created { get; set; } = null!;
        public string Message { get; set; } = null!;
    }
}