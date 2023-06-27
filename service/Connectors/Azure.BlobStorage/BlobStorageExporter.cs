using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using SensorData.Api.Collector.Connectors.Sensor.Community;
using SensorData.Api.Collector.Services;

namespace SensorData.Api.Collector.Connectors.Netatmo;

public class BlobStorageExporter : BackgroundService
{
    public static string AzureBlobConnectionString { get; set; }
    public static string ContainerName { get; set; }
    public static string AzureAccessToken { get; set; }
    private readonly MongoWrapper _mongoClient;
    private readonly ILogger<BlobStorageExporter> _logger;
    private Timer? _timer = null;

    public BlobStorageExporter(MongoWrapper mongoClient, ILogger<BlobStorageExporter> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Execute once a day.
        _timer = new Timer(async state => await ExportToBlobStorage(state), new TimerContext { Token = stoppingToken }, 0, (int)TimeSpan.FromDays(1).TotalMilliseconds);

        return Task.CompletedTask;
    }

    private async Task ExportToBlobStorage(object state)
    {
        if (state is not TimerContext ctx)
            return;

        try
        {
            // Fetch mongo db sensor readings.
            var sensorReadings = await _mongoClient.GetSensorReadings();

            // Export
            var blobServiceClient = new BlobServiceClient(AzureBlobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

            var now = DateTime.Now;
            var fileName = $"sensor_readings_{now.Day}_{now.Month}_{now.Year}.csv";

            _logger.LogInformation($"Uploading {sensorReadings.Count()} entries to {fileName}.");

            var blobClient = containerClient.GetBlobClient(fileName);

            CsvExportHelper.ExportToCsv(sensorReadings, fileName);

            using (var ms = File.OpenRead(fileName))
            {
                await blobClient.UploadAsync(ms, overwrite: false);
            }

            // If everything went smoothly, delete mongo entries.
            await _mongoClient.DeleteSensorReadings();

            _logger.LogInformation("Starting to clean up file.");

            // Clean up file.
            File.Delete(fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Something went wrong while exporting csv data.");
        }
    }
}

public class CsvExportHelper
{
    public static void ExportToCsv(IEnumerable<MongoDbTimeSeriesReading> readings, string path)
    {
        using var writer = new StreamWriter(path);
        using (var csv = new CsvWriter(writer, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)))
        {
            csv.WriteRecords(readings);
        }
    }
}