using Microsoft.EntityFrameworkCore;
using Middagsasen.Planner.Api.Core;
using Middagsasen.Planner.Api.Data;

namespace Middagsasen.Planner.Api.Services.Weather
{
    public class WeatherService(PlannerDbContext dbContext)
    {
        public async Task SaveMeasurementValues(IEnumerable<MeasurementValueRequest> measurementValues)
        {
            foreach (var measurementValue in measurementValues)
            {
                dbContext.WeatherMeasurementValues.Add(new WeatherMeasurementValue
                {
                    WeatherLocationId = measurementValue.WeatherLocationId,
                    WeatherMeasurementId = (int)measurementValue.WeatherMeasurementId,
                    MeasuredValue = measurementValue.MeasuredValue,
                    MeasuredTime = measurementValue.MeasuredAt
                });
            }
            await dbContext.SaveChangesAsync();
        }

        public async Task<IEnumerable<LocationMeasurementResponse>> GetLocationMeasurements(LocationMeasurementRequest request)
        {
            var start = request.Start;
            var end = request.End.AddSeconds(1);
            var query = dbContext.WeatherLocations
                .Include(l => l.Values.Where(v => start < v.MeasuredTime && v.MeasuredTime < end))
                    .ThenInclude(v => v.WeatherMeasurement)
                    .AsNoTracking();

            var result = await query.ToListAsync();

            var response = new List<LocationMeasurementResponse>();
            foreach (var location in result)
            {
                if (location.Values.Count == 0) continue;

                var groupedValues = location.Values.GroupBy(v => v.WeatherMeasurementId);
                var measurements = new List<MeasurementResponse>();

                foreach (var group in groupedValues)
                {
                    var measurementName = group.First().WeatherMeasurement.MeasurementLabel;
                    var measurementResponse = new MeasurementResponse { MeasurementName = measurementName };
                    var values = group
                        .OrderBy(v => v.MeasuredTime)
                        .Select(value => new MeasurementValueResponse
                        {
                            Value = value.MeasuredValue,
                            MeasuredTime = value.MeasuredTime.AsUtc().ToIsoString()
                        })
                        .ToList();
                    measurementResponse.Values = values;
                    measurements.Add(measurementResponse);
                }
                var locationResponse = new LocationMeasurementResponse
                {
                    LocationName = location.LocationName,
                    Measurements = measurements
                };
                response.Add(locationResponse);
            }
            return response;
        } 
    }

    public enum MeasurementType
    {
        WindSpeed = 6,
        Temperature = 7,
        Humidity = 8,
        WindDirection = 10,
    }
}
