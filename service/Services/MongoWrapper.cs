using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using SensorData.Api.Collector.Utils;

namespace SensorData.Api.Collector.Services;

public class MongoWrapper
{
    private readonly ILogger<MongoWrapper> _logger;
    private MongoClient _client;
    private IMongoDatabase _database;

    public MongoWrapper(ILogger<MongoWrapper> logger)
    {
        _client = new MongoClient(MongoConfig.MongoSettings);
        _database = _client.GetDatabase("test");
        _logger = logger;
    }

    public async Task SaveSensorReadings(IEnumerable<MongoDbTimeSeriesReading> sensorReadings)
    {
        _logger.LogInformation("Saving {Count} sensor readings to the mongo db.", sensorReadings.Count());

        var collection = _database.GetCollection<MongoDbTimeSeriesReading>("sensor_readings_timeseries");

        await collection.InsertManyAsync(sensorReadings);
    }

    public async Task<IEnumerable<MongoDbTimeSeriesReading>> GetSensorReadings()
    {
        _logger.LogInformation("Retrieving sensor readings from mongo db.");

        var collection = _database.GetCollection<MongoDbTimeSeriesReading>("sensor_readings_timeseries");

        return await collection.Find(_ => true).ToListAsync();
    }

    public async Task DeleteSensorReadings()
    {
        _logger.LogInformation("Starting to delete sensor readings from mongo db.");

        var collection = _database.GetCollection<MongoDbTimeSeriesReading>("sensor_readings_timeseries");

        await collection.DeleteManyAsync(_ => true);
    }
}

public record MongoDbTimeSeriesReading
{
    [BsonId]
    public string Id { get; set; }

    // Readings
    // Temp in celcius.
    public double? Temperature { get; set; }

    // Percentage of humidity.
    public double? Humidity { get; set; }

    // Pressure in mbar
    public double? Pressure { get; set; }

    // Meta data
    public DateTime Timestamp { get; set; }
    public MongoDbMetaData Metadata { get; set; }
}

public record MongoDbMetaData
{
    public GeoJsonPoint<GeoJson3DCoordinates> Location { get; set; }
    public string? SensorType { get; set; }

    /// <summary>
    /// Can be either netatmo or sensor.community
    /// </summary>
    public string? Provider { get; set; }
    public string? NetatmoSensorId { get; set; }
    public string? SensorCommunitySensorType { get; set; }
}
