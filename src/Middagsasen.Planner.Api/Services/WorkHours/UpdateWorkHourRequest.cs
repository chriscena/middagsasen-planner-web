namespace Middagsasen.Planner.Api.Services.WorkHours
{
    /// <summary>
    /// Delvis oppdatering av en timeføring. Kun felter som er satt (ikke null) endres.
    /// </summary>
    /// <remarks>
    /// <see cref="ApprovalStatus"/> brukes kun til å godkjenne (1) eller avslå (2).
    /// Siden et utelatt felt ikke kan skilles fra eksplisitt null, kan «Ingen status»
    /// ikke settes her — bruk <c>PATCH /api/WorkHours/{id}/ApprovedBy</c> for det.
    /// Tilsvarende kan ikke <see cref="EndTime"/> eller <see cref="Description"/> nullstilles
    /// via dette endepunktet (tom streng er tillatt for beskrivelse).
    /// </remarks>
    public class UpdateWorkHourRequest
    {
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Description { get; set; }
        public int? ApprovalStatus { get; set; }
    }
}
