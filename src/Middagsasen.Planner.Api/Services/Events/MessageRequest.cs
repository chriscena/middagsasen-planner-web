namespace Middagsasen.Planner.Api.Services.Events
{
    public class MessageRequest
    {
        public int EventResourceId { get; set; }
        public string Message { get; set; } = null!;
        public int CreatedBy { get; set; }
    }
}