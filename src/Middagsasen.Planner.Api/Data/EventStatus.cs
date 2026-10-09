namespace Middagsasen.Planner.Api.Data
{
    public class EventStatus
    {
        public int EventId { get; set; }
        public DateTime StartTime { get; set; }
        public int ShiftCount { get; set; }
        public int Users { get; set; }
        public int MissingStaff { get; set; }
    }
}