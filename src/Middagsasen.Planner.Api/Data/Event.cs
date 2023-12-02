namespace Middagsasen.Planner.Api.Data
{
    public class Event
    {
        public int EventId { get; set; }
        public string Name { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public ICollection<EventResource> Resources { get; set; } = new HashSet<EventResource>();
    }
}
