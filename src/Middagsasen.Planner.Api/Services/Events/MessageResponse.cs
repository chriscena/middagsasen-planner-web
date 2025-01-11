namespace Middagsasen.Planner.Api.Services.Events
{
    public class MessageResponse
    {
        public int Id { get; set; }
        public int EventResourceId { get; set; }
        public ShiftUserResponse CreatedBy { get; set; } 
        public string Created { get; set; }
        public string Message { get; set; }
    }
}