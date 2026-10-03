namespace Middagsasen.Planner.Api.Data
{
    public class EventResourceUser
    {
        public int EventResourceUserId { get; set; }
        public int UserId { get; set; }
        public int EventResourceId { get; set;}
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Comment { get; set; }

        public User User { get; set; } = null!;
        public EventResource Resource { get; set; } = null!;

        /// <summary>Timeføringer knyttet til vakta. Vakta kan ikke slettes så lenge den har timeføringer.</summary>
        public ICollection<WorkHour> WorkHours { get; set; } = new List<WorkHour>();
    }
}
