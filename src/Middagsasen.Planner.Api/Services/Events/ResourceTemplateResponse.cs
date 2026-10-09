using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceTemplateResponse
    {
        public int Id { get; set; }
        public ResourceTypeResponse ResourceType { get; set; } = null!;
        /// <summary>Oppgavens start, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string StartTime { get; set; } = null!;
        /// <summary>Oppgavens slutt, norsk lokal tid uten sone (<c>yyyy-MM-ddTHH:mm</c>).</summary>
        public string EndTime { get; set; } = null!;
        public int ShiftCount { get; set; }
    }
}