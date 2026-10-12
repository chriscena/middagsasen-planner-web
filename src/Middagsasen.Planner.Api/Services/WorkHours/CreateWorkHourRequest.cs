using System.ComponentModel.DataAnnotations;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    /// <summary>
    /// Oppretter en timeføring. Eier er innlogget bruker med mindre <see cref="UserId"/> er satt.
    /// Bare admin kan opprette for en annen bruker og opprette som godkjent (ellers 403).
    /// </summary>
    public class CreateWorkHourRequest
    {
        /// <summary>
        /// Eieren av føringen. Utelatt (eller null) = innlogget bruker. Bare admin kan angi en annen bruker (ellers 403).
        /// Ukjent eller inaktiv bruker gir 404.
        /// </summary>
        public int? UserId { get; set; }
        [Required]
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
        /// <summary>
        /// true = opprett og godkjenn i samme lagring («Lagre og godkjenn»). Utelatt, null eller false = åpen føring.
        /// Bare admin kan sette true (ellers 403).
        /// </summary>
        public bool? Approve { get; set; }
    }
}
