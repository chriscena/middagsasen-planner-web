using Middagsasen.Planner.Api.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Middagsasen.Planner.Api.Services.Weather;

internal class WeatherDataCollector(IServiceScopeFactory scopeFactory, ILogger<WeatherDataCollector> logger) : IHostedService, IDisposable
{
    private static readonly HttpClient _httpClient = new();
    private bool _running = false;
    private Timer? _timer;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Weather data collection started.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        await Task.CompletedTask;
    }

    JsonSerializerSettings JsonSerializerSettings => new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
    };

    public async void DoWork(object? state)
    {
        if (_running) return;
        logger.LogInformation("Fetching weather data.");
        try
        {
            using var scope = scopeFactory.CreateScope();
            var weatherService = scope.ServiceProvider.GetRequiredService<WeatherService>();

            _running = true;
            var request = new HttpRequestMessage(HttpMethod.Get, "https://webapi.ubibot.com/channels?account_key=a34fb31647911819e04ea39c61af4fb7");
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var deviceInfo = content != null ? JsonConvert.DeserializeObject<Root>(content, JsonSerializerSettings) : null;
                if (deviceInfo == null) return;

                var valuesToSave = new List<MeasurementValueRequest>();

                foreach (var channel in deviceInfo.Channels)
                {
                    var sensorData = channel.LastValues != null ? JsonConvert.DeserializeObject<SensorData>(channel.LastValues, JsonSerializerSettings) : null;

                    if (sensorData == null) continue;

                    var channelId = int.Parse(channel.ChannelId);
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
            _running = false;
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

public class DeviceData
{
    public string Field3 { get; set; }
    public string Field4 { get; set; }
    public string Field5 { get; set; }
    public string Field6 { get; set; }
    public string Field7 { get; set; }
    public string Field8 { get; set; }
    public string Field9 { get; set; }
    public string Field10 { get; set; }
    public string Field11 { get; set; }
    public string Field12 { get; set; }
    public string Field13 { get; set; }
    public string Field14 { get; set; }
    public string Field15 { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
    public string Name { get; set; }
    public bool PublicFlag { get; set; }
    public object Tags { get; set; }
    public object Url { get; set; }
    public string Metadata { get; set; }
    public object MetadataD { get; set; }
    public object Description { get; set; }
    public string TrafficOut { get; set; }
    public string TrafficIn { get; set; }
    public string Status { get; set; }
    public string Timezone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Usage { get; set; }
    public string LastEntryId { get; set; }
    public DateTime LastEntryDate { get; set; }
    public string ProductId { get; set; }
    public string DeviceId { get; set; }
    public object ChannelIcon { get; set; }
    public string LastIp { get; set; }
    public DateTime AttachedAt { get; set; }
    public string Firmware { get; set; }
    public string FullDump { get; set; }
    public DateTime ActivatedAt { get; set; }
    public string Serial { get; set; }
    public string MacAddress { get; set; }
    public string FullDumpLimit { get; set; }
    public string Cali { get; set; }
}

public class Channel
{
    public string ChannelId { get; set; }
    public string Field1 { get; set; }
    public string Field2 { get; set; }
    public string Field3 { get; set; }
    public string Field4 { get; set; }
    public string Field5 { get; set; }
    public string Field6 { get; set; }
    public string Field7 { get; set; }
    public string Field8 { get; set; }
    public string Field9 { get; set; }
    public string Field10 { get; set; }
    public string Field11 { get; set; }
    public string Field12 { get; set; }
    public string Field13 { get; set; }
    public string Field14 { get; set; }
    public string Field15 { get; set; }
    public string Latitude { get; set; }
    public string Longitude { get; set; }
    public string Name { get; set; }
    public bool PublicFlag { get; set; }
    public object Tags { get; set; }
    public object Url { get; set; }
    public string Metadata { get; set; }
    public object MetadataD { get; set; }
    public object Description { get; set; }
    public string TrafficOut { get; set; }
    public string TrafficIn { get; set; }
    public string Status { get; set; }
    public string Timezone { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Usage { get; set; }
    public string LastEntryId { get; set; }
    public DateTime LastEntryDate { get; set; }
    public string ProductId { get; set; }
    public string DeviceId { get; set; }
    public object ChannelIcon { get; set; }
    public string LastIp { get; set; }
    public DateTime AttachedAt { get; set; }
    public string Firmware { get; set; }
    public string FullDump { get; set; }
    public DateTime ActivatedAt { get; set; }
    public string Serial { get; set; }
    public string MacAddress { get; set; }
    public string FullDumpLimit { get; set; }
    public string Cali { get; set; }
    public string SizeOut { get; set; }
    public string SizeStorage { get; set; }
    public string PlanCode { get; set; }
    public string AllowChannelFields { get; set; }
    public DateTime PlanStart { get; set; }
    public object PlanEnd { get; set; }
    public DateTime BillStart { get; set; }
    public DateTime BillEnd { get; set; }
    public string LastValues { get; set; }
    public string Vconfig { get; set; }
    public string Vpref { get; set; }
    public string Sensors { get; set; }
    public string SensorsMapping { get; set; }
    public object HubEntries { get; set; }
    public object MaxFields { get; set; }
    public object Battery { get; set; }
    public string VprefFrom { get; set; }
    public string Net { get; set; }
    public object CIconBase { get; set; }
    public object StatusDate { get; set; }
    public string FullSerial { get; set; }
    public object TriggeringRules { get; set; }
}

public class Root
{
    public string Result { get; set; }
    public DateTime ServerTime { get; set; }
    public List<Channel> Channels { get; set; }
    public List<object> VirtualFields { get; set; }
}