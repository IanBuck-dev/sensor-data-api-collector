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
builder.Services.AddHostedService<SensorCommunityService>();
builder.Services.AddHostedService<NetatmoService>();
builder.Services.AddHostedService<BlobStorageExporter>();

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

// Setup MongoDb
var mongoUserPw = Environment.GetEnvironmentVariable("SENSOR_MONGODB_PW");

var settings = MongoClientSettings.FromConnectionString($"mongodb+srv://mongouser:{mongoUserPw}@cluster.gbiuwau.mongodb.net/?retryWrites=true&w=majority");
settings.ServerApi = new ServerApi(ServerApiVersion.V1);
MongoConfig.MongoSettings = settings;

// Setup Azure BlobStorage
var blobStorageConnectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_STORAGE_CONNECTION_STRING");
BlobStorageExporter.AzureBlobConnectionString = blobStorageConnectionString;
BlobStorageExporter.ContainerName = "sensor-data";

await app.RunAsync();