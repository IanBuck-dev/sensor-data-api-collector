// See https://aka.ms/new-console-template for more information
using SensorData.Api.Collector.Connectors.Sensor.Community;

Console.WriteLine("Starting api collectors...");

var builder = WebApplication.CreateBuilder(args);

// Add config for connectors (auth, interval etc.)

// Add services
builder.Services.AddHttpClient();
builder.Services.AddHostedService<SensorCommunityService>();

// Add recurring jobs as workers


var app = builder.Build();

// Todo: Add health check

await app.RunAsync();