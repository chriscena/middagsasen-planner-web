namespace Middagsasen.Planner.Api.Services.Events
{
    public class EventTemplateRequest
    {
        public string Name { get; set; } = null!;
        public string EventName { get; set; } = null!;
        /// <summary>Klokkeslett. JSON: <c>"HH:mm"</c> (sekunder kan tas med).</summary>
        public required TimeOnly StartTime { get; set; }
        /// <summary>Klokkeslett, som <see cref="StartTime"/>. Før start betyr neste døgn.</summary>
        public required TimeOnly EndTime { get; set; }
        public IEnumerable<ResourceTemplateRequest> ResourceTemplates { get; set; } = null!;
        /// <summary>
        /// Anleggskravene. Ved lagring erstattes hele settet (endret antall oppdateres, nye legges til, manglende fjernes).
        /// <c>null</c> betyr at kravene ikke endres ved oppdatering (og ingen krav ved opprettelse), slik at klienter som ikke
        /// sender feltet, ikke fjerner dem. Tom liste fjerner alle.
        /// </summary>
        public IEnumerable<CompetencyRequirementRequest>? CompetencyRequirements { get; set; }
    }
}