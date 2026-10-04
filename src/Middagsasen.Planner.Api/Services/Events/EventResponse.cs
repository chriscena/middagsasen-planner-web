namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventResponse
    {
        public int Id { get; internal set; }
        public string Name { get; internal set; } = null!;
        public string? Description { get; internal set; }
        /// <summary>Arrangementets start, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string StartTime { get; internal set; } = null!;
        /// <summary>Arrangementets slutt, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string EndTime { get; internal set; } = null!;
        public IEnumerable<ResourceResponse> Resources { get; set; } = null!;
    }
}