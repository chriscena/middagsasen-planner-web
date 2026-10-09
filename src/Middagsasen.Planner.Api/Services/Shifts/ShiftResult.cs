using Middagsasen.Planner.Api.Services.Events;
using Middagsasen.Planner.Api.Services.ResourceTypes;

namespace Middagsasen.Planner.Api.Services.Shifts
{
    /// <summary>Svaret fra alle skriveoperasjonene på vakter.</summary>
    public class ShiftResult
    {
        /// <summary>
        /// Hele oppgaven etter endringen, med vakter og flagg for innlogget bruker, mappet likt som i
        /// <c>GET api/events</c>. Klienten kan erstatte oppgaven i cachen med denne.
        /// </summary>
        public ResourceResponse Resource { get; internal set; } = null!;

        /// <summary>
        /// Opplæringen (eieren av vakta på oppgavens vakttype) hvis den ble opprettet eller endret, ellers <c>null</c>.
        /// Gjelder alle oppgaver av samme vakttype, så klienten kan oppdatere <c>mustAnswerTraining</c>/<c>needsTraining</c> der.
        /// </summary>
        public TrainingResponse? ChangedTraining { get; internal set; }

        /// <summary>
        /// Advarsler til brukeren om ting som ikke stoppet endringen, f.eks. at SMS til trenerne feilet. Tom liste når alt gikk bra.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; internal set; } = [];
    }
}
