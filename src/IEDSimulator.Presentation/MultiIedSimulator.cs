using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Services;
using IEDSimulator.Infrastructure.Repositories;

namespace IEDSimulator.Presentation
{
    public class MultiIedSimulator
    {
        private readonly List<IedSimulatorService> _iedSimulators = new List<IedSimulatorService>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Dictionary<string, int> _updateCounters = new Dictionary<string, int>();

        public void AddIedSimulator(string icdFilePath, string stationName)
        {
            var dataPointRepository = new IEC61850DataPointRepository(icdFilePath);
            var simulatorService = new IedSimulatorService(dataPointRepository);

            var configuration = new IedConfiguration
            {
                StationName = stationName,
                DeviceName = $"Simulator_{stationName}",
                IcdFilePath = icdFilePath
            };

            // Tambahkan titik data default
            configuration.DataPoints.Add(new DataPoint 
            { 
                Id = $"{stationName}/MMXU1.Amp.phsA$MX", 
                Name = $"Arus Fasa A - {stationName}", 
                Type = "float" 
            });
            configuration.DataPoints.Add(new DataPoint 
            { 
                Id = $"{stationName}/MMXU1.Amp.phsB$MX", 
                Name = $"Arus Fasa B - {stationName}", 
                Type = "float" 
            });

            // Inisialisasi simulator
            simulatorService.InitializeAsync(configuration).Wait();
            simulatorService.DataPointChanged += OnDataPointChanged;

            _iedSimulators.Add(simulatorService);
        }

        public async Task StartSimulationAsync()
        {
            var tasks = new List<Task>();

            foreach (var simulator in _iedSimulators)
            {
                tasks.Add(Task.Run(async () => 
                {
                    try 
                    {
                        await simulator.StartSimulationAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Kesalahan pada simulator: {ex.Message}");
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        public async Task StopSimulationAsync()
        {
            _cancellationTokenSource.Cancel();
            
            var stopTasks = new List<Task>();
            foreach (var simulator in _iedSimulators)
            {
                stopTasks.Add(simulator.StopSimulationAsync());
            }

            await Task.WhenAll(stopTasks);

            // Cetak statistik
            Console.WriteLine("\nStatistik Simulator:");
            foreach (var counter in _updateCounters)
            {
                Console.WriteLine($"- {counter.Key}: {counter.Value} pembaruan");
            }
        }

        private void OnDataPointChanged(object sender, Core.Models.DataPoint dataPoint)
        {
            var simulatorService = sender as IedSimulatorService;
            var stationName = simulatorService?.Configuration?.StationName ?? "Unknown";

            // Cetak informasi perubahan
            Console.WriteLine(
                $"[{DateTime.Now}] Perubahan Titik Data: \n" +
                $"  Stasiun: {stationName}\n" +
                $"  ID: {dataPoint.Id}\n" +
                $"  Nama: {dataPoint.Name}\n" +
                $"  Nilai: {dataPoint.Value:F2}"
            );

            // Tambahkan penghitung update
            if (!_updateCounters.ContainsKey(stationName))
            {
                _updateCounters[stationName] = 0;
            }
            _updateCounters[stationName]++;
        }
    }
}