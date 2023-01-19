using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using SensorData.Api.Collector.Models;
using SensorData.Api.Collector.Services;

namespace SensorData.Api.Collector.Connectors.Sensor.Community;

public class SensorCommunityService : BackgroundService
{
    private const string BaseUrl = "https://data.sensor.community/static/v2/data.json";
    private readonly HttpClient _httpClient;
    private readonly MongoWrapper _mongoClient;
    private readonly JsonSerializerOptions _options;
    private Timer? _timer = null;

    public SensorCommunityService(HttpClient httpClient, MongoWrapper mongoClient)
    {
        _httpClient = httpClient;
        _mongoClient = mongoClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUtility.UrlEncode("Heat-Islands Detection Uni Hamburg 6buck@informatik.uni-hamburg.de"));

        _options = new JsonSerializerOptions();
        _options.PropertyNameCaseInsensitive = true;
        _options.Converters.Add(new DateTimeConverterUsingDateTimeParse());
        _options.Converters.Add(new DecimalConverterUsingDecimalParse());
    }

    public async Task GetSensorReadings(object state)
    {
        if (state is not TimerContext ctx)
            return;

        // Get result from api.
        // Includes all sensor readings of all sensors averaged over the last 5 min.
        var result = await _httpClient.GetStringAsync(BaseUrl, ctx.Token);
        // var result = await File.ReadAllTextAsync("readings.json");

        var readings = JsonSerializer.Deserialize<List<SCSensorReading>>(result, _options);

        // Filter out everything we don't need
        var readingsToSave = readings.Where(r => LocationFilter(r) && SensorFilter(r)).ToList();
        var readingsToReturn = new List<MongoDbTimeSeriesReading>(readingsToSave.Count);

        foreach (var item in readingsToSave)
        {
            readingsToReturn.Add(item.ToMongoDbTimeSeriesReading());
        }

        if (readingsToReturn.Any())
        {
            await _mongoClient.SaveSensorReadings(readingsToReturn);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new Timer(async state => await GetSensorReadings(state), new TimerContext { Token = stoppingToken }, 0, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);

        return Task.CompletedTask;
    }

    private bool LocationFilter(SCSensorReading r)
    {
        // Bad Oldeslohe: 53.79604022151624N, 10.355624923959386E
        // Lüneburg: 53.24894326638651, 10.369662928285342
        // Elmshorn: 53.7886691669084, 9.659963814592672
        // Tostedt: 53.27786506750316, 9.669322484225408

        return r.Location != null && r.Location.Country == "DE" && r.Location.Indoor == 0
             && r.Location.Latitude <= 53.8m && r.Location.Latitude >= 53.2m
             && r.Location.Longitude <= 10.4m && r.Location.Longitude >= 9.6m;
    }

    private bool SensorFilter(SCSensorReading r)
    {
        return r.Sensor != null && r.Sensor.SensorType != null && (r.Sensor.SensorType.Name == "BME280" || r.Sensor.SensorType.Name != "DHT22")
            && r.SensorDataValues != null && r.SensorDataValues.Any(x => x.ValueType == "temperature" || x.ValueType == "pressure" || x.ValueType == "humidity");
    }
}

#region Models

public record SCSensorReading
{
    public long Id { get; set; }
    // public object? SamplingRate { get; set; }
    public DateTime Timestamp { get; set; }
    public SCLocation Location { get; set; }
    public SCSensor Sensor { get; set; }
    public List<SCSensorDataReading> SensorDataValues { get; set; }

    public MongoDbTimeSeriesReading ToMongoDbTimeSeriesReading()
    {
        var sensorReading = new MongoDbTimeSeriesReading()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Timestamp = Timestamp,
            Metadata = new MongoDbMetaData()
            {
                Provider = "sensor.community",
                Location = new(new GeoJson3DCoordinates(
                    (double?)Location.Longitude ?? default,
                    (double?)Location.Latitude ?? default,
                    (double?)Location.Altitude ?? default)),
                SensorCommunitySensorType = Sensor.SensorType.Name
            }
        };

        
        var temperatureDataReading = SensorDataValues.FirstOrDefault(s => s.ValueType == "temperature");

        if (temperatureDataReading != null && double.TryParse(temperatureDataReading.Value.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedTempReading))
        {
            sensorReading.Temperature = parsedTempReading;
        }

        var pressureDataReading = SensorDataValues.FirstOrDefault(s => s.ValueType == "pressure");

        if (pressureDataReading != null && double.TryParse(pressureDataReading.Value.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedPressureDataReading))
        {
            // need to devide by 100 to be milliBar
            sensorReading.Pressure = parsedPressureDataReading / 100;
        }

        var humidityDataReading = SensorDataValues.FirstOrDefault(s => s.ValueType == "humidity");

        if (humidityDataReading != null && double.TryParse(humidityDataReading.Value.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedHumidityDataReading))
        {
            sensorReading.Humidity = parsedHumidityDataReading;
        }

        return sensorReading;
    }
}

public record SCLocation
{
    public long Id { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Latitude { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Longitude { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Altitude { get; set; }
    public string Country { get; set; }

    [JsonPropertyName("exact_location")]
    public int ExactLocation { get; set; }
    public int Indoor { get; set; }
}

public record SCSensor
{
    public long Id { get; set; }
    public string Pin { get; set; }

    [JsonPropertyName("sensor_type")]
    public SCSensorType SensorType { get; set; }
}

public record SCSensorType
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string Manufacturer { get; set; }
}

public record SCSensorDataReading
{
    public long Id { get; set; }
    public object Value { get; set; }

    [JsonPropertyName("value_type")]
    public string ValueType { get; set; }
}

internal record TimerContext
{
    public CancellationToken Token { get; set; }
}

public class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return DateTime.Parse(reader.GetString() ?? string.Empty);
        }
        else if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var time))
            {
                // if 'IsFormatInSeconds' is unspecified, then deduce the correct type based on whether it can be represented in the allowed .net DateTime range
                if (time >= _unixMinSeconds && time < _unixMaxSeconds)
                    return DateTimeOffset.FromUnixTimeSeconds(time).LocalDateTime;
                return DateTimeOffset.FromUnixTimeMilliseconds(time).LocalDateTime;
            }
        }

        return default;
    }

    private static readonly long _unixMinSeconds = DateTimeOffset.MinValue.ToUnixTimeSeconds() - DateTimeOffset.UnixEpoch.ToUnixTimeSeconds(); // -62_135_596_800
    private static readonly long _unixMaxSeconds = DateTimeOffset.MaxValue.ToUnixTimeSeconds() - DateTimeOffset.UnixEpoch.ToUnixTimeSeconds(); // 253_402_300_799

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => throw new NotSupportedException();
}

public class DecimalConverterUsingDecimalParse : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        var input = reader.GetString() ?? string.Empty;

        if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return default;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}


#endregion