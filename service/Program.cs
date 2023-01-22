// See https://aka.ms/new-console-template for more information
using MongoDB.Driver;
using SensorData.Api.Collector.Connectors.Netatmo;
using SensorData.Api.Collector.Connectors.Sensor.Community;
using SensorData.Api.Collector.Services;
using SensorData.Api.Collector.Utils;

Console.WriteLine("Starting api collectors...");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MongoWrapper>();

// Add services
builder.Services.AddHttpClient();
// Todo: add mongodb upload for sensor community
builder.Services.AddHostedService<SensorCommunityService>();
builder.Services.AddHostedService<NetatmoService>();

// Add recurring jobs as workers

// Setup logging.
builder.Logging.AddConsole();

var app = builder.Build();

// Add config for connectors (auth, interval etc.)
NetatmoService.NetatmoAccessToken = Environment.GetEnvironmentVariable("NETATMO_ACCESS_TOKEN");
NetatmoService.NetatmoRefreshToken = Environment.GetEnvironmentVariable("NETATMO_REFRESH_TOKEN");
NetatmoService.NetatmoClientId = Environment.GetEnvironmentVariable("NETATMO_CLIENT_ID");
NetatmoService.NetatmoClientSecret = Environment.GetEnvironmentVariable("NETATMO_CLIENT_SECRET");

if (NetatmoService.NetatmoAccessToken is null)
    throw new Exception("Failed to fetch environment variables.");

Console.WriteLine("Getting env variables...");
Console.WriteLine($"{nameof(NetatmoService.NetatmoAccessToken)}: {NetatmoService.NetatmoAccessToken}");
Console.WriteLine($"{nameof(NetatmoService.NetatmoRefreshToken)}: {NetatmoService.NetatmoRefreshToken}");
Console.WriteLine($"{nameof(NetatmoService.NetatmoClientId)}: {NetatmoService.NetatmoClientId}");
Console.WriteLine($"{nameof(NetatmoService.NetatmoClientSecret)}: {NetatmoService.NetatmoClientSecret}");

// Setup MongoDb
var mongoUserPw = Environment.GetEnvironmentVariable("SENSOR_MONGODB_PW");

var settings = MongoClientSettings.FromConnectionString($"mongodb+srv://mongouser:{mongoUserPw}@cluster.gbiuwau.mongodb.net/?retryWrites=true&w=majority");
settings.ServerApi = new ServerApi(ServerApiVersion.V1);
MongoConfig.MongoSettings = settings;

// configure logging.

// Todo: Add health check

await app.RunAsync();