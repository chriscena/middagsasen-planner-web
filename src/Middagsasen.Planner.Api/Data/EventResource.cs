namespace Middagsasen.Planner.Api.Data
{
    public class EventResource
    {
        public int EventResourceId { get; set; }
        public int EventId { get; set; }
        public int ResourceTypeId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int MinimumStaff { get; set; }

        public Event Event { get; set; } = null!;
        public ResourceType ResourceType { get; set; } = null!;

        public ICollection<EventResourceUser> Shifts { get; set; } = new HashSet<EventResourceUser>();
    }
}
