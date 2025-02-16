#!/bin/bash

# Create the main src directory
mkdir -p src

# --- IEDSimulator.Core ---
mkdir -p src/IEDSimulator.Core/Models
mkdir -p src/IEDSimulator.Core/Interfaces
mkdir -p src/IEDSimulator.Core/Services

# DataPoint.cs
cat << EOF > src/IEDSimulator.Core/Models/DataPoint.cs
using System;

namespace IEDSimulator.Core.Models
{
    public class DataPoint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public dynamic Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
EOF

# IedConfiguration.cs
cat << EOF > src/IEDSimulator.Core/Models/IedConfiguration.cs
using System.Collections.Generic;

namespace IEDSimulator.Core.Models
{
    public class IedConfiguration
    {
        public string StationName { get; set; }
        public string DeviceName { get; set; }
        public string IcdFilePath { get; set; }
        public List<DataPoint> DataPoints { get; set; } = new List<DataPoint>();
    }
}
EOF

# IDataPointRepository.cs
cat << EOF > src/IEDSimulator.Core/Interfaces/IDataPointRepository.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;

namespace IEDSimulator.Core.Interfaces
{
    public interface IDataPointRepository
    {
        Task<DataPoint> GetDataPointAsync(string id);
        Task<IEnumerable<DataPoint>> GetAllDataPointsAsync();
        Task UpdateDataPointAsync(DataPoint dataPoint);
        Task AddDataPointAsync(DataPoint dataPoint);
    }
}
EOF

# IIedService.cs
cat << EOF > src/IEDSimulator.Core/Interfaces/IIedService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;

namespace IEDSimulator.Core.Interfaces
{
    public interface IIedService
    {
        Task InitializeAsync(IedConfiguration configuration);
        Task StartSimulationAsync();
        Task StopSimulationAsync();
        Task<IEnumerable<DataPoint>> GetCurrentDataPointsAsync();
        event EventHandler<DataPoint> DataPointChanged;
    }
}
EOF

# IedSimulatorService.cs
cat << EOF > src/IEDSimulator.Core/Services/IedSimulatorService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Interfaces;
using IEDSimulator.Core.Models;

namespace IEDSimulator.Core.Services
{
    public class IedSimulatorService : IIedService
    {
        private readonly IDataPointRepository _dataPointRepository;
        private IedConfiguration _configuration;
        private bool _isRunning;

        public event EventHandler<DataPoint> DataPointChanged;

        public IedSimulatorService(IDataPointRepository dataPointRepository)
        {
            _dataPointRepository = dataPointRepository ?? 
                throw new ArgumentNullException(nameof(dataPointRepository));
        }

        public virtual async Task InitializeAsync(IedConfiguration configuration)
        {
            _configuration = configuration ?? 
                throw new ArgumentNullException(nameof(configuration));

            foreach (var dataPoint in configuration.DataPoints)
            {
                await _dataPointRepository.AddDataPointAsync(dataPoint);
            }
        }

        public virtual async Task StartSimulationAsync()
        {
            if (_isRunning)
                return;

            _isRunning = true;
            await SimulateDataChangesAsync();
        }

        public virtual Task StopSimulationAsync()
        {
            _isRunning = false;
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<DataPoint>> GetCurrentDataPointsAsync()
        {
            return await _dataPointRepository.GetAllDataPointsAsync();
        }

        protected virtual async Task SimulateDataChangesAsync()
        {
            while (_isRunning)
            {
                var dataPoints = await _dataPointRepository.GetAllDataPointsAsync();
                foreach (var dataPoint in dataPoints)
                {
                    dataPoint.Value = GenerateSimulatedValue(dataPoint);
                    dataPoint.Timestamp = DateTime.UtcNow;

                    await _dataPointRepository.UpdateDataPointAsync(dataPoint);
                    
                    DataPointChanged?.Invoke(this, dataPoint);
                }

                await Task.Delay(1000);
            }
        }

        private dynamic GenerateSimulatedValue(DataPoint dataPoint)
        {
            return dataPoint.Type switch
            {
                "float" => SimulateFloatValue(),
                "int" => SimulateIntValue(),
                "bool" => SimulateBoolValue(),
                _ => dataPoint.Value
            };
        }

        private float SimulateFloatValue() => 
            (float)new Random().NextDouble() * 100;

        private int SimulateIntValue() => 
            new Random().Next(0, 1000);

        private bool SimulateBoolValue() => 
            new Random().Next(2) == 1;
    }
}
EOF


# --- IEDSimulator.Infrastructure ---
mkdir -p src/IEDSimulator.Infrastructure/Repositories
mkdir -p src/IEDSimulator.Infrastructure/Services

# IEC61850DataPointRepository.cs
cat << EOF > src/IEDSimulator.Infrastructure/Repositories/IEC61850DataPointRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Interfaces;
using IEC61850.NET.client; // Sesuaikan dengan library Anda

namespace IEDSimulator.Infrastructure.Repositories
{
    public class IEC61850DataPointRepository : IDataPointRepository
    {
        private readonly Dictionary<string, DataPoint> _dataPoints = new Dictionary<string, DataPoint>();
        private readonly IEC61850ClientAPI _clientApi;

        public IEC61850DataPointRepository(string icdFilePath)
        {
            if (string.IsNullOrEmpty(icdFilePath))
                throw new ArgumentNullException(nameof(icdFilePath));

            _clientApi = new IEC61850ClientAPI(icdFilePath);
        }

        public Task<DataPoint> GetDataPointAsync(string id) =>
            Task.FromResult(_dataPoints.TryGetValue(id, out var dataPoint) ? dataPoint : null);

        public Task<IEnumerable<DataPoint>> GetAllDataPointsAsync() =>
            Task.FromResult(_dataPoints.Values.AsEnumerable());

        public Task UpdateDataPointAsync(DataPoint dataPoint)
        {
            if (_dataPoints.ContainsKey(dataPoint.Id))
            {
                _dataPoints[dataPoint.Id] = dataPoint;
                WriteDataPointToServer(dataPoint);
            }
            return Task.CompletedTask;
        }

        public Task AddDataPointAsync(DataPoint dataPoint)
        {
            if (!_dataPoints.ContainsKey(dataPoint.Id))
            {
                _dataPoints.Add(dataPoint.Id, dataPoint);
            }
            return Task.CompletedTask;
        }

        private void WriteDataPointToServer(DataPoint dataPoint)
        {
            switch (dataPoint.Type)
            {
                case "float":
                    _clientApi.WriteFloatValue(dataPoint.Id, Convert.ToSingle(dataPoint.Value));
                    break;
                case "int":
                    _clientApi.WriteIntValue(dataPoint.Id, Convert.ToInt32(dataPoint.Value));
                    break;
                case "bool":
                    _clientApi.WriteBoolValue(dataPoint.Id, Convert.ToBoolean(dataPoint.Value));
                    break;
            }
        }
    }
}
EOF


# --- IEDSimulator.Presentation ---
mkdir -p src/IEDSimulator.Presentation

# Program.cs
cat << EOF > src/IEDSimulator.Presentation/Program.cs
using System;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Infrastructure.Repositories;

namespace IEDSimulator.Presentation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try 
            {
                string icdFilePath = "path/to/your/model.icd";

                var dataPointRepository = new IEC61850DataPointRepository(icdFilePath);
                var simulatorService = new IEC61850SimulatorService(dataPointRepository, icdFilePath);

                var configuration = new IedConfiguration
                {
                    StationName = "TestStation",
                    DeviceName = "SimulatedIED",
                    IcdFilePath = icdFilePath
                };

                await simulatorService.InitializeAsync(configuration);
                simulatorService.DataPointChanged += OnDataPointChanged;

                await simulatorService.StartSimulationAsync();

                Console.WriteLine("IED Simulator running. Press any key to stop.");
                Console.ReadKey();

                await simulatorService.StopSimulationAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Simulation error: {ex.Message}");
            }
        }

        private static void OnDataPointChanged(object sender, Core.Models.DataPoint dataPoint)
        {
            Console.WriteLine(
                $"Data Point Changed: " +
                $"ID: {dataPoint.Id}, " +
                $"Value: {dataPoint.Value}, " +
                $"Timestamp: {dataPoint.Timestamp}"
            );
        }
    }
}
EOF

echo "Directory structure, files, and content created successfully."

