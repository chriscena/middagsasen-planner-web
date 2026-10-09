using System.Text.Json.Serialization;
using Middagsasen.Planner.Api.Core;

namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        /// <summary>Lokal tid uten sone. JSON: <c>"yyyy-MM-ddTHH:mm"</c> (sekunder kan tas med). Sone og år utenfor 1900–9998 avvises.</summary>
        [JsonConverter(typeof(LocalDateTimeConverter))]
        public required DateTime StartTime { get; set; }
        /// <summary>Lokal tid uten sone, som <see cref="StartTime"/>. Før start betyr neste døgn.</summary>
        [JsonConverter(typeof(LocalDateTimeConverter))]
        public required DateTime EndTime { get; set; }
        public IEnumerable<ResourceRequest> Resources { get; set; } = null!;
        /// <summary>
        /// Anleggskravene. Ved lagring erstattes hele settet (endret antall oppdateres, nye legges til, manglende fjernes).
        /// <c>null</c> betyr at kravene ikke endres ved oppdatering (og ingen krav ved opprettelse), slik at klienter som ikke
        /// sender feltet, ikke fjerner dem. Tom liste fjerner alle.
        /// </summary>
        public IEnumerable<CompetencyRequirementRequest>? CompetencyRequirements { get; set; }
    }
}