using Middagsasen.Planner.Api.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Middagsasen.Planner.Api.Services.Weather;

internal class WeatherDataCollector(
    IServiceScopeFactory scopeFactory,
    ILogger<WeatherDataCollector> logger,
    IHttpClientFactory httpClientFactory,
    WeatherSettings weatherSettings) : IHostedService, IDisposable
{
    private readonly WeatherSettings _settings = weatherSettings;
    private int _running;
    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Weather data collection started.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        await Task.CompletedTask;
    }

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
    };

    public async void DoWork(object? state)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        try
        {
            logger.LogInformation("Fetching weather data.");
            using var scope = scopeFactory.CreateScope();
            var weatherService = scope.ServiceProvider.GetRequiredService<WeatherService>();

            var httpClient = httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.UbibotBaseUrl}channels?account_key={_settings.UbibotAccountKey}");
            var response = await httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var deviceInfo = content != null ? JsonConvert.DeserializeObject<Root>(content, JsonSerializerSettings) : null;
                if (deviceInfo == null) return;

                var valuesToSave = new List<MeasurementValueRequest>();

                foreach (var channel in deviceInfo.Channels ?? [])
                {
                    var sensorData = channel.LastValues != null ? JsonConvert.DeserializeObject<SensorData>(channel.LastValues, JsonSerializerSettings) : null;

                    if (sensorData == null || !int.TryParse(channel.ChannelId, out var channelId)) continue;

                    if (sensorData.Field6?.Value != null && sensorData.Field6?.CreatedAt != null)
                        valuesToSave.Add(new MeasurementValueRequest
                        {
                            WeatherLocationId = channelId,
                            WeatherMeasurementId = MeasurementType.WindSpeed,
                            MeasuredValue = sensorData.Field6.Value.Value,
                            MeasuredAt = sensorData.Field6.CreatedAt.Value
                        });

                    if (sensorData.Field7?.Value != null && sensorData.Field7?.CreatedAt != null)
                        valuesToSave.Add(new MeasurementValueRequest
                        {
                            WeatherLocationId = channelId,
                            WeatherMeasurementId = MeasurementType.Temperature,
                            MeasuredValue = sensorData.Field7.Value.Value,
                            MeasuredAt = sensorData.Field7.CreatedAt.Value
                        });

                    if (sensorData.Field8?.Value != null && sensorData.Field8?.CreatedAt != null)
                        valuesToSave.Add(new MeasurementValueRequest
                        {
                            WeatherLocationId = channelId,
                            WeatherMeasurementId = MeasurementType.Humidity,
                            MeasuredValue = sensorData.Field8.Value.Value,
                            MeasuredAt = sensorData.Field8.CreatedAt.Value
                        });

                    if (sensorData.Field10?.Value != null && sensorData.Field10?.CreatedAt != null)
                        valuesToSave.Add(new MeasurementValueRequest
                        {
                            WeatherLocationId = channelId,
                            WeatherMeasurementId = MeasurementType.WindDirection,
                            MeasuredValue = sensorData.Field10.Value.Value,
                            MeasuredAt = sensorData.Field10.CreatedAt.Value
                        });
                }

                if (valuesToSave.Count > 0)
                    await weatherService.SaveMeasurementValues(valuesToSave);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching weather data.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }

    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Weather data collection stopped.");
        _timer?.Change(Timeout.Infinite, 0);
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

public class Field
{
    public decimal? Value { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class Wifi
{
    public string? Value { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class SensorData
{
    public Field? Log { get; set; }
    public Field? Field6 { get; set; }
    public Field? Field1 { get; set; }
    public Field? Field2 { get; set; }
    public Field? Field3 { get; set; }
    public Field? Field7 { get; set; }
    public Field? Field8 { get; set; }
    public Field? Field4 { get; set; }
    public Wifi? Wifi { get; set; }
    public int? SensorsCountdown { get; set; }
    public Field? Field10 { get; set; }
    public Field? Field13 { get; set; }
}

public class Channel
{
    public string? ChannelId { get; set; }
    public string? LastValues { get; set; }
}

public class Root
{
    public List<Channel>? Channels { get; set; }
}
