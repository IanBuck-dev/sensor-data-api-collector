using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SensorData.Api.Collector.Connectors.Sensor.Community;
using SensorData.Api.Collector.Services;

namespace tests.Connectors;

public class SensorCommunityConnectorTests
{
    [Fact]
    public async Task ToMongoSensorReadings_ShouldParse_CorrectReadings()
    {
        // Given
        var input = await File.ReadAllTextAsync("sensorCommunityResponse.json");

        var options = new JsonSerializerOptions();
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new DateTimeConverterUsingDateTimeParse());
        options.Converters.Add(new DecimalConverterUsingDecimalParse());

        var result = JsonSerializer.Deserialize<IEnumerable<SCSensorReading>>(input, options);

        // When
        var sensorReadings = result.Select(x => x.ToMongoDbTimeSeriesReading());

        // Then
        sensorReadings.Count().Should().Be(6);

        // Todo: Verify that for each sensor reading type the reading is correct.
    }
}
