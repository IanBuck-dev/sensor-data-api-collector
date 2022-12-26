using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensorData.Api.Collector.Models;

namespace SensorData.Api.Collector.Services;

public interface ISensorDataCollector
{
    Task<IEnumerable<SensorDataReading>> GetSensorReadings();
}
