namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        /// <summary>Lokal tid uten sone. JSON: <c>"yyyy-MM-ddTHH:mm"</c> (sekunder kan tas med).</summary>
        public required DateTime StartTime { get; set; }
        /// <summary>Lokal tid uten sone, som <see cref="StartTime"/>. Før start betyr neste døgn.</summary>
        public required DateTime EndTime { get; set; }
        public IEnumerable<ResourceRequest> Resources { get; set; } = null!;
    }
}