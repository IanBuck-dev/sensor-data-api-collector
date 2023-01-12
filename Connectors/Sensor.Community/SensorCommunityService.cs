using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using SensorData.Api.Collector.Models;

namespace SensorData.Api.Collector.Connectors.Sensor.Community;

public class SensorCommunityService : BackgroundService
{
    private const string BaseUrl = "https://data.sensor.community/static/v2/data.json";
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _options;
    private Timer? _timer = null;

    public SensorCommunityService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUtility.UrlEncode("Heat-Islands Detection Uni Hamburg 6buck@informatik.uni-hamburg.de"));

        _options = new JsonSerializerOptions();
        _options.PropertyNameCaseInsensitive = true;
        _options.Converters.Add(new DateTimeConverterUsingDateTimeParse());
        _options.Converters.Add(new DecimalConverterUsingDoubleParse());
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
        var readingsToReturn = new List<SensorDataReading>(readingsToSave.Count);

        // Todo: Save readings to the DB
        Console.WriteLine($"Readings from {DateTime.UtcNow.ToString("s")}");

        foreach (var item in readingsToSave)
        {
            readingsToReturn.Add(item.ToSensorDataReading());
            Console.WriteLine(item);
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
        return true;
        // return r.Sensor != null && r.Sensor.SensorType != null && r.Sensor.SensorType.Name == "SDS011";
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

    public SensorDataReading ToSensorDataReading()
    {
        var reading = new SensorDataReading()
        {
            Location = new Location()
            {
                Latitude = Location.Latitude ?? default,
                Longitude = Location.Longitude ?? default,
                Altitude = Location.Altitude ?? default
            }
        };

        // Map temperature readings.
        if (Sensor.SensorType.Name == "BME280")
        {
            var temperatureDataReading = SensorDataValues.FirstOrDefault(s => s.ValueType == "temperature");

            if (temperatureDataReading != null && double.TryParse(temperatureDataReading.Value.ToString(), out var parsedTempReading))
            {
                reading.Temperature = new TemperatureReading()
                {
                    Value = parsedTempReading,
                    Measurement = "Celsius",
                    SensorType = "BME280"
                };
            }
        }

        return reading;
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
        return DateTime.Parse(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class DecimalConverterUsingDoubleParse : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
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