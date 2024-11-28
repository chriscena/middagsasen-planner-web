namespace Middagsasen.Planner.Api.Services.Weather
{
    public class LocationMeasurementRequest
    {
        public DateTime Start { get; set; } = DateTime.UtcNow;
        public DateTime End { get; set; } = DateTime.UtcNow.AddMinutes(120);
    }
}