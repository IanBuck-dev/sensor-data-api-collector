using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SensorData.Api.Collector.Models;

public record SensorDataReading
{
    public Location Location { get; set; }
    public TemperatureReading Temperature { get; set; }
}

public record TemperatureReading
{
    public double Value { get; set; }
    public string Measurement { get; set; } = "Celsius";
    public string SensorType { get; set; }
}

public record Location
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Altitude { get; set; }
}
