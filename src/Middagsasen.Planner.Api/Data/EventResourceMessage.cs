namespace Middagsasen.Planner.Api.Data
{
    public class EventResourceMessage
    {
        public int EventResourceMessageId { get; set; }
        public int EventResourceId { get; set;}
        public string Message { get; set; } = null!;
        public DateTime Created { get; set; }
        public int CreatedBy { get; set; }

        public User CreatedByUser { get; set; } = null!;
        public EventResource Resource { get; set; } = null!;
    }
}
