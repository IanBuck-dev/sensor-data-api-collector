// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var builder = WebApplication.CreateBuilder(args);

// Add config for connectors (auth, interval etc.)

// Add services
// Todo: Configure http client and add 


// Add recurring jobs as workers

var app = builder.Build();

// Todo: Add health check

await app.RunAsync();