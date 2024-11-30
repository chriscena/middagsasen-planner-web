namespace Middagsasen.Planner.Api.Services.Weather
{
    public class LocationMeasurementResponse
    {
        public string LocationName { get; set; } = null!;
        public IEnumerable<MeasurementResponse> Measurements { get; set; } = [];
    }

    public class MeasurementResponse
    {
        public string MeasurementName { get; set; } = null!;
        public string? Unit { get; set; }
        public IEnumerable<MeasurementValueResponse> Values { get; set; } = [];
        public MeasurementValueResponse? LastValue { get; internal set; }
    }

    public class MeasurementValueResponse
    {
        public decimal Value { get; set; }
        public string MeasuredTime { get; set; } = null!;

    }
}