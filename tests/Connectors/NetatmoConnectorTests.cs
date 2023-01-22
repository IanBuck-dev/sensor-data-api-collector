using System.Text.Json;
using FluentAssertions;
using SensorData.Api.Collector.Connectors.Netatmo;
using SensorData.Api.Collector.Connectors.Sensor.Community;

namespace tests.Connectors;

public class NetatmoConnectorTests
{
    [Fact]
    public async Task ToMongoSensorReadings_ShouldParse_CorrectReadings()
    {
        // Given
        var input = await File.ReadAllTextAsync("netatmoResponse.json");

        var options = new JsonSerializerOptions();
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new UnixToDateTimeConverter());
        options.Converters.Add(new DecimalConverterUsingDecimalParse());

        var netatmoResult = JsonSerializer.Deserialize<NetatmoApiResponse>(input, options);

        // When
        var sensorReadings = netatmoResult.ToMongoSensorReadings();

        // Then
        sensorReadings.Count().Should().Be(48);

        // Todo: Verify that for each sensor reading type the reading is correct.
    }
}
