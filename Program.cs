// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var builder = WebApplication.CreateBuilder(args);

// Add services
// Add recurring jobs as workers

var app = builder.Build();

// Todo: Add health check

await app.RunAsync();