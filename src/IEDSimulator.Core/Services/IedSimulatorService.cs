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
        private bool _isRunning;
        
        // Properti untuk konfigurasi
        public IedConfiguration Configuration { get; private set; }

        public event EventHandler<DataPoint> DataPointChanged;

        public IedSimulatorService(IDataPointRepository dataPointRepository)
        {
            _dataPointRepository = dataPointRepository ?? 
                throw new ArgumentNullException(nameof(dataPointRepository));
        }

        // Metode InitializeAsync tunggal
        public virtual async Task InitializeAsync(IedConfiguration configuration)
        {
            Configuration = configuration ?? 
                throw new ArgumentNullException(nameof(configuration));

            // Tambahkan semua titik data dari konfigurasi ke repositori
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