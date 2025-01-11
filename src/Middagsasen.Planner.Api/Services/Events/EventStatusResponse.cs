namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventStatusResponse
    {
        public string Date { get; set; } = null!;
        public bool IsMissingStaff { get; set; }
    }
}