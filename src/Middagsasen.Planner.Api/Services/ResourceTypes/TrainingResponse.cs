namespace Middagsasen.Planner.Api.Services.ResourceTypes
{
    public class TrainingResponse
    {
        public int Id { get; set; }
        /// <summary>Brukeren opplæringen gjelder.</summary>
        public int UserId { get; set; }
        public int ResourceTypeId { get; set; }
        public string? ResourceTypeName { get; set; }
        /// <summary>Når opplæringen ble bekreftet, UTC-tidspunkt med sone (<c>yyyy-MM-ddTHH:mm:ssZ</c>).</summary>
        public string? Confirmed { get; set; }
        public int? ConfirmedById { get; set; }
        public string? ConfirmedByName { get; set; }
        public bool? TrainingComplete { get; set; }
    }
}