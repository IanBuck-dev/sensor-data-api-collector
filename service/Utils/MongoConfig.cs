using MongoDB.Driver;

namespace SensorData.Api.Collector.Utils;

public static class MongoConfig
{
    public static MongoClientSettings? MongoSettings { get; set; }
}
