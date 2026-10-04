namespace Middagsasen.Planner.Api.Services.Events
{
    public class ResourceRequest
    {
        public int? Id { get; set; }
        public int ResourceTypeId { get; set; }
        /// <summary>Klokkeslett. JSON: <c>"HH:mm"</c> (sekunder kan tas med). Døgnet bestemmes ut fra vaktlista.</summary>
        public required TimeOnly StartTime { get; set; }
        /// <summary>Klokkeslett, som <see cref="StartTime"/>. Før start betyr neste døgn.</summary>
        public required TimeOnly EndTime { get; set; }
        public int MinimumStaff { get; set; }
        /// <summary>
        /// <see cref="MinimumStaff"/> slik den var da skjemaet ble lastet. Gjelder bare eksisterende ressurser (med <see cref="Id"/>)
        /// ved oppdatering av vaktlista: endringen (<c>MinimumStaff - OriginalMinimumStaff</c>) legges på verdien som er lagret nå,
        /// slik at ledige plasser andre har lagt til eller fjernet i mellomtiden, ikke overskrives (#151). Lik verdi betyr uendret.
        /// <c>null</c> betyr at <see cref="MinimumStaff"/> settes som absolutt verdi (bakoverkompatibelt for klienter som ikke sender feltet).
        /// Ignoreres for nye ressurser.
        /// </summary>
        public int? OriginalMinimumStaff { get; set; }
        public bool IsDeleted { get; set; }
    }
}