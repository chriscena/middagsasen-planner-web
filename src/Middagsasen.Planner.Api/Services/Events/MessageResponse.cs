namespace Middagsasen.Planner.Api.Services.Events
{
    public class MessageResponse
    {
        public int Id { get; set; }
        public int EventResourceId { get; set; }
        public ShiftUserResponse CreatedBy { get; set; } = null!;
        /// <summary>Når meldingen ble skrevet, UTC-tidspunkt med sone (<c>yyyy-MM-ddTHH:mm:ssZ</c>).</summary>
        public string Created { get; set; } = null!;
        public string Message { get; set; } = null!;
    }
}