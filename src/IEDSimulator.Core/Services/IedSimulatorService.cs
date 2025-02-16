using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Interfaces;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Enums;
using System.Linq;

namespace IEDSimulator.Core.Services
{
    public class IedSimulatorService : IIedService
    {
        private readonly IDataPointRepository _dataPointRepository;
        private bool _isRunning;
        private readonly object _lockObject = new object();

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
                // Gunakan lock untuk operasi yang mempengaruhi shared state
                lock (_lockObject)
                {
                    var dataPoints = _dataPointRepository.GetAllDataPointsAsync().Result;
                    foreach (var dataPoint in dataPoints)
                    {
                        dataPoint.Value = GenerateSimulatedValue(dataPoint);
                        dataPoint.Timestamp = DateTime.UtcNow;

                        _dataPointRepository.UpdateDataPointAsync(dataPoint).Wait();
                        
                        DataPointChanged?.Invoke(this, dataPoint);
                    }
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

        public async Task<bool> ExecuteControlAsync(string controlId, ControlOperation operation)
        {
            try 
            {
                // Cari titik data yang sesuai
                var dataPoint = Configuration.DataPoints
                    .FirstOrDefault(dp => dp.Id == controlId);

                if (dataPoint == null)
                {
                    Console.WriteLine($"Kontrol tidak ditemukan: {controlId}");
                    return false;
                }

                // Eksekusi kontrol
                var result = await _dataPointRepository.ExecuteControlAsync(controlId, operation);

                // Update status jika berhasil
                if (result)
                {
                    dataPoint.Value = operation == ControlOperation.Open ? 1 : 0;
                    dataPoint.Timestamp = DateTime.UtcNow;
                    
                    // Trigger event perubahan
                    DataPointChanged?.Invoke(this, dataPoint);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kesalahan eksekusi kontrol: {ex.Message}");
                return false;
            }
        }


        private float SimulateFloatValue() => 
            (float)new Random().NextDouble() * 100;

        private int SimulateIntValue() => 
            new Random().Next(0, 1000);

        private bool SimulateBoolValue() => 
            new Random().Next(2) == 1;
    }
}