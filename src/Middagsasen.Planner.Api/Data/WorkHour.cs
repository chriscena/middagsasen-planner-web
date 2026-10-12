namespace Middagsasen.Planner.Api.Data
{
    public class WorkHour
    {
        public int WorkHourId { get; set; }
        public int UserId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
        public int? ApprovedBy { get; set; }
        public ApprovalStatus? ApprovalStatus { get; set; }
        public DateTime? ApprovedTime { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedTime { get; set; }
        /// <summary>
        /// Hvem som førte timene (ikke nødvendigvis eieren <see cref="UserId"/>). Settes ved opprettelse.
        /// Nullable fordi rader laget av API-versjonen før kolonnen fantes mangler verdien til deploy-skriptet fyller den ut.
        /// </summary>
        public int? CreatedBy { get; set; }
        /// <summary>Når føringen ble opprettet (UTC). Nullable av samme grunn som <see cref="CreatedBy"/>.</summary>
        public DateTime? CreatedTime { get; set; }

        public virtual User? ApprovedByUser { get; set; }
        public virtual User? ModifiedByUser { get; set; }
        public virtual User? CreatedByUser { get; set; }
    }
}
