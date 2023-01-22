using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SensorData.Api.Collector.Connectors.Sensor.Community;
using SensorData.Api.Collector.Services;

namespace SensorData.Api.Collector.Connectors.Netatmo;

public class NetatmoService : BackgroundService
{
    public static string NetatmoAccessToken { get; set; }
    public static string NetatmoRefreshToken { get; set; }
    public static string NetatmoClientId { get; set; }
    public static string NetatmoClientSecret { get; set; }
    public DateTime ExpiresIn { get; set; } = DateTime.UtcNow;
    private const string BaseUrl = "https://api.netatmo.com/api/getpublicdata?lat_ne=53.7960&lon_ne=10.3556&lat_sw=53.2778&lon_sw=9.6693&filter=false";
    private const string RefreshUrl = "https://api.netatmo.com/oauth2/token";
    private readonly HttpClient _httpClient;
    private readonly MongoWrapper _mongoClient;
    private readonly ILogger<NetatmoService> _logger;
    private readonly JsonSerializerOptions _options;
    private Timer? _timer = null;

    public NetatmoService(HttpClient httpClient, MongoWrapper mongoClient, ILogger<NetatmoService> logger)
    {
        _httpClient = httpClient;
        _mongoClient = mongoClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUtility.UrlEncode("Heat-Islands Detection Uni Hamburg 6buck@informatik.uni-hamburg.de"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {NetatmoAccessToken}");

        _options = new JsonSerializerOptions();
        _options.PropertyNameCaseInsensitive = true;
        _options.Converters.Add(new UnixToDateTimeConverter());
        _options.Converters.Add(new DecimalConverterUsingDecimalParse());
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new Timer(async state => await GetSensorReadings(state), new TimerContext { Token = stoppingToken }, 0, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

        return Task.CompletedTask;
    }

    public async Task GetSensorReadings(object state)
    {
        if (state is not TimerContext ctx)
            return;

        try
        {
            // Get result from api.
            // Includes all sensor readings of all sensors averaged over the last 5 min.
            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // Refresh access token.
                var refreshUrl = $"{RefreshUrl}?grant_type=refresh_token&refresh_token={NetatmoRefreshToken}&client_id={NetatmoClientId}&client_secret={NetatmoClientSecret}";
                var refreshRequest = new HttpRequestMessage(HttpMethod.Post, refreshUrl);

                var refreshResponse = await _httpClient.SendAsync(refreshRequest);

                if (refreshResponse.IsSuccessStatusCode)
                {
                    var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<NetatmoRefreshResponse>();

                    NetatmoAccessToken = refreshResult.AccessToken;
                    NetatmoRefreshToken = refreshResult.RefreshToken;
                    ExpiresIn = DateTime.UtcNow.AddSeconds(refreshResult.ExpiresIn);

                    request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
                    response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception("Failed to fetch netatmo data dispite access token refresh.");
                    }
                }
                else
                {
                    throw new Exception("Failed to refresh access token for netatmo.");
                }
            }

            var netatmoResult = await response.Content.ReadFromJsonAsync<NetatmoApiResponse>(_options);

            var sensorReadings = netatmoResult.ToMongoSensorReadings();

            _logger.LogInformation("Created {Count} sensor readings from netatmo.", sensorReadings.Count());

            if (sensorReadings != null && sensorReadings.Any())
            {
                await _mongoClient.SaveSensorReadings(sensorReadings);
            }
        }
        catch (Exception ex)
        {
            // Do not log exception on shutdown.
            if (ex is OperationCanceledException)
                throw;

            // Do not throw exceptions to keep the hosted service running.
            _logger.LogError(ex, "Error while processing sensor readings for netatmo collector.");
        }
    }
}

#region Models

public record NetatmoRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
}

public record NetatmoApiResponse
{
    public string Status { get; set; }

    [JsonPropertyName("time_server")]
    public DateTime TimeServer { get; set; }
    public List<NASensorReading> Body { get; set; }

    public IEnumerable<MongoDbTimeSeriesReading> ToMongoSensorReadings()
    {
        var sensorReadings = new List<MongoDbTimeSeriesReading>(Body.Count);

        foreach (var netatmoSensorReading in Body)
        {
            // create temperature reading
            foreach (var measure in netatmoSensorReading.Measures.EnumerateObject())
            {
                // Case temperature and humidity
                if (measure.Value.TryGetProperty("type", out var measureTypes))
                {
                    var types = measureTypes.EnumerateArray().Select(x => x.GetString()).ToList();

                    if (measure.Value.TryGetProperty("res", out var sensorMeasurements))
                    {
                        var sensorIds = sensorMeasurements.EnumerateObject();
                        foreach (var sensorId in sensorIds)
                        {
                            var x = 0;

                            foreach (var reading in sensorId.Value.EnumerateArray())
                            {
                                var value = reading.GetDouble();

                                var type = types[x];
                                x++;

                                if (type != "temperature" && type != "humidity" && type != "pressure")
                                    continue;

                                var sensorReading = new MongoDbTimeSeriesReading()
                                {
                                    Id = ObjectId.GenerateNewId().ToString(),
                                    Timestamp = TimeServer,
                                    Metadata = new MongoDbMetaData()
                                    {
                                        Provider = "netatmo",
                                        Location = new(new GeoJson3DCoordinates(
                                            netatmoSensorReading.Place.Location[0],
                                            netatmoSensorReading.Place.Location[1],
                                            netatmoSensorReading.Place.Altitude)),
                                        NetatmoSensorId = measure.Name
                                    }
                                };

                                sensorReadings.Add(sensorReading);

                                if (type == "temperature")
                                {
                                    sensorReading.Temperature = value;
                                }
                                else if (type == "humidity")
                                {
                                    sensorReading.Humidity = value;
                                }
                                else if (type == "pressure")
                                {
                                    sensorReading.Pressure = value;
                                }
                            }
                        }
                    }
                }
            }
        }

        return sensorReadings;
    }
}

public record NASensorReading
{
    [JsonPropertyName("_id")]
    public string Id { get; set; }
    public NAPlace Place { get; set; }
    public JsonElement Measures { get; set; }
    public List<NAModuleType> ModuleTypes { get; set; }
}

public record NAPlace
{
    public string Timezone { get; set; }
    public string Country { get; set; }
    public int Altitude { get; set; }

    /// <summary>
    /// Longitude, Latitude
    /// </summary>
    public List<double> Location { get; set; }
}

public record NAMeasure
{
    [JsonPropertyName("mac_address_NAModule1")]
    public NAModule1 MacAddressNAModule1 { get; set; }

    [JsonPropertyName("mac_address_NAMain")]
    public NAModuleMain MacAddressNAMain { get; set; }

    [JsonPropertyName("mac_address_NAModule2")]
    public NAModule2 MacAddressNAModule2 { get; set; }

    [JsonPropertyName("mac_address_NAModule3")]
    public NAModule3 MacAddressNAModule3 { get; set; }
}

public record NAModule1
{
    public NAModule1Response Res { get; set; }

    /// <summary>
    /// [temperature]
    /// </summary>
    public List<string> Type { get; set; }
}

public record NAModule1Response
{
    [JsonPropertyName("time_stamp")]
    public List<decimal> TimeStamp { get; set; }
}

public record NAModuleMain
{
    public NAModule1Response Res { get; set; }

    /// <summary>
    /// Pressure
    /// </summary>
    public string Type { get; set; }
}

public record NAModule2
{
    [JsonPropertyName("wind_strengh")]
    public decimal WindStrength { get; set; }

    [JsonPropertyName("wind_angle")]
    public decimal WindAngle { get; set; }

    [JsonPropertyName("gust_strenght")]
    public decimal GustStrength { get; set; }

    [JsonPropertyName("gust_angle")]
    public decimal GustAngle { get; set; }

    [JsonPropertyName("wind_timeutc")]
    public decimal WindTimeUtc { get; set; }
}

public record NAModule3
{
    [JsonPropertyName("rain_60min")]
    public decimal Rain60Min { get; set; }

    [JsonPropertyName("rain_24h")]
    public decimal Rain24h { get; set; }

    [JsonPropertyName("rain_live")]
    public decimal RainLive { get; set; }

    [JsonPropertyName("rain_timeutc")]
    public decimal RainTimeUtc { get; set; }
}

public record NAModuleType
{
    [JsonPropertyName("mac_adress")]
    public string MacAdress { get; set; }
}

public class UnixToDateTimeConverter : JsonConverter<DateTime>
{
    public override bool HandleNull => true;
    public bool? IsFormatInSeconds { get; set; } = null;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TryGetInt64(out var time))
        {
            // if 'IsFormatInSeconds' is unspecified, then deduce the correct type based on whether it can be represented in the allowed .net DateTime range
            if (IsFormatInSeconds == true || IsFormatInSeconds == null && time > _unixMinSeconds && time < _unixMaxSeconds)
                return DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;
            return DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotSupportedException();

    private static readonly long _unixMinSeconds = DateTimeOffset.MinValue.ToUnixTimeSeconds() - DateTimeOffset.UnixEpoch.ToUnixTimeSeconds(); // -62_135_596_800
    private static readonly long _unixMaxSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds() - DateTimeOffset.UnixEpoch.ToUnixTimeSeconds(); // 253_402_300_799
}

#endregion