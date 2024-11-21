namespace Middagsasen.Planner.Api.Data
{
    public class WeatherLocation
    {
        public int WeatherLocationId { get; set; }
        public string LocationName { get; set; } = null!;
        public ICollection<WeatherMeasurementValue> Values { get; set; } = new HashSet<WeatherMeasurementValue>();
    }

    public class WeatherMeasurement
    {
        public int WeatherMeasurementId { get; set; }
        public string MeasurementLabel { get; set; } = null!;
        public ICollection<WeatherMeasurementValue> Values { get; set; } = new HashSet<WeatherMeasurementValue>();
    }

    public class WeatherMeasurementValue 
    {
        public int WeatherMeasurementValueId { get; set; }
        public int WeatherMeasurementId { get; set; }
        public WeatherMeasurement WeatherMeasurement { get; set; } = null!;
        public int WeatherLocationId { get; set; }
        public WeatherLocation WeatherLocation { get; set; } = null!;

        public decimal MeasuredValue { get; set; }
        public DateTime MeasuredTime { get; set; }
    }
}
