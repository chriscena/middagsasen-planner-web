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
    }

    public enum MeasurementType
    {
        WindSpeed = 6,
        Temperature = 7,
        Humidity = 8,
        WindDirection = 10,
    }
}
