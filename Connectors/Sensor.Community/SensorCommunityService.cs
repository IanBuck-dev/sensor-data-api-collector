using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensorData.Api.Collector.Models;
using SensorData.Api.Collector.Services;

namespace SensorData.Api.Collector.Connectors.Sensor.Community;

public class SensorCommunityService : ISensorDataCollector
{
    private const string BaseUrl = "https://data.sensor.community/static/v2/data.json";
    private readonly HttpClient _httpClient;

    public SensorCommunityService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        // Todo: configure user-agent
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Heat-Islands Detection Uni Hamburg 6buck@informatik.uni-hamburg.de");
    }

    public Task<IEnumerable<SensorDataReading>> GetSensorReadings()
    {
        
    }

    private HttpRequestMessage GetRequestMessage()
    {
        var message = new HttpRequestMessage(HttpMethod.Get, BaseUrl);

        return message;
    }
}
