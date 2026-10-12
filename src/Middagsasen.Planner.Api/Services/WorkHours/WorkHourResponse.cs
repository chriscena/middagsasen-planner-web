using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.WorkHours
{
    public class WorkHourResponse
    {
        public int WorkHourId { get; internal set; }
        public int UserId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? Hours { get; set; }
        public string? Description { get; set; }
        public int? ApprovedBy { get; set; }
        public string? ApprovedByName { get; set; }
        public DateTime? ApprovedTime { get; set; }
        /// <summary>Godkjent, avslått, eller null = åpen (ubehandlet).</summary>
        public ApprovalStatus? ApprovalStatus { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        public DateTime? ModifiedTime { get; set; }
        /// <summary>
        /// Hvem som førte timene (ikke nødvendigvis eieren <see cref="UserId"/>). Kan mangle (null) på føringer
        /// som ble laget av eldre API-versjoner og ennå ikke er fylt ut av deploy-skriptet.
        /// </summary>
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        /// <summary>Når føringen ble opprettet (UTC). Kan mangle av samme grunn som <see cref="CreatedBy"/>.</summary>
        public DateTime? CreatedTime { get; set; }

        /// <summary>Innlogget bruker kan endre starttid, sluttid og beskrivelse (eier eller admin, og føringen er åpen).</summary>
        public bool CanEdit { get; set; }
        /// <summary>Innlogget bruker kan slette føringen (samme regel som <see cref="CanEdit"/>).</summary>
        public bool CanDelete { get; set; }
        /// <summary>Innlogget bruker kan godkjenne eller avslå føringen (admin, og føringen er åpen).</summary>
        public bool CanApprove { get; set; }
        /// <summary>Innlogget bruker kan sette «Ingen status» og dermed låse opp føringen (admin, og føringen er godkjent eller avslått).</summary>
        public bool CanResetStatus { get; set; }
    }
}
