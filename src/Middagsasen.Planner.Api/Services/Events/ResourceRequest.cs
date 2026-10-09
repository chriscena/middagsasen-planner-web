using System.ComponentModel.DataAnnotations;

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
        [Range(0, int.MaxValue)]
        public required int ShiftCount { get; set; }
        /// <summary>
        /// <see cref="ShiftCount"/> slik den var da skjemaet ble lastet. Gjelder bare eksisterende ressurser (med <see cref="Id"/>)
        /// ved oppdatering av vaktlista, og sammenlignes med verdien som er lagret nå (#151):
        /// lik <see cref="ShiftCount"/> betyr uendret i skjemaet, og ingenting skrives (ledige plasser andre har lagt til eller
        /// fjernet i mellomtiden, beholdes). Ellers settes <see cref="ShiftCount"/> hvis lagret verdi fortsatt er lik denne
        /// (eller allerede er lik <see cref="ShiftCount"/>), og hvis noen andre har endret den, avvises lagringen med 409.
        /// <c>null</c> betyr at <see cref="ShiftCount"/> settes som absolutt verdi (bakoverkompatibelt for klienter som ikke sender feltet).
        /// Ignoreres for nye ressurser.
        /// </summary>
        public int? OriginalShiftCount { get; set; }
        public bool IsDeleted { get; set; }
    }
}