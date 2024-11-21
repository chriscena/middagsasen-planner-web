
namespace Middagsasen.Planner.Api.Services.Weather
{
    public class MeasurementValueRequest
    {
        public int WeatherLocationId { get; set; }
        public MeasurementType WeatherMeasurementId { get; set; }
        public decimal MeasuredValue { get; set; }
        public DateTime MeasuredAt { get; set; }
    }
}