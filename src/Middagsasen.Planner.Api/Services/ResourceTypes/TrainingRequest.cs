namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class TrainingRequest
    {
        /// <summary>
        /// Id til eksisterende opplæring som skal oppdateres. Utelates (eller 0) for ny opplæring.
        /// </summary>
        public int? Id { get; set; }
        public int ResourceTypeId { get; set; }
        public int UserId { get; set; }
        public DateTime StartTime { get; set; }
        public bool? TrainingCompleted { get; set; }
    }
}