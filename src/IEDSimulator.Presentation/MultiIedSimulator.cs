using System;
using System.IO; 
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Services;
using IEDSimulator.Infrastructure.Repositories;

namespace IEDSimulator.Presentation
{
    public class MultiIedSimulator
    {
        private readonly ConcurrentBag<IedSimulatorService> _iedSimulators = new ConcurrentBag<IedSimulatorService>();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private ConcurrentDictionary<string, int> _updateCounters = new ConcurrentDictionary<string, int>();

        // Tambahkan properti untuk mengontrol logging
        public bool IsLoggingEnabled { get; private set; } = false;

        // Metode untuk mengaktifkan logging
        public void EnableLogging()
        {
            IsLoggingEnabled = true;
        }

        // Metode untuk menonaktifkan logging
        public void DisableLogging()
        {
            IsLoggingEnabled = false;
        }

        // Modifikasi metode GetSimulatorByStationName
        public IedSimulatorService? GetSimulatorByStationName(string stationName)
        {
            return _iedSimulators.FirstOrDefault(
                simulator => simulator.Configuration?.StationName?.Equals(stationName, StringComparison.OrdinalIgnoreCase) == true
            );
        }

        public void AddIedSimulator(string icdFilePath)
        {
            // Parse XML
            XDocument xmlDoc = XDocument.Load(icdFilePath);
            XNamespace scl = "http://www.iec.ch/61850/2003/SCL";

            // Ekstrak nama IED dari header atau IED element dengan fallback
            string stationName = ExtractStationName(xmlDoc, scl);

            var dataPointRepository = new IEC61850DataPointRepository(icdFilePath);
            var simulatorService = new IedSimulatorService(dataPointRepository);

            var configuration = new IedConfiguration
            {
                StationName = stationName,
                DeviceName = $"Simulator_{stationName}",
                IcdFilePath = icdFilePath
            };

            // Temukan dan tambahkan titik data dari file ICD
            var dataPoints = DiscoverDataPointsFromIcdFile(xmlDoc, scl, stationName);
            configuration.DataPoints.AddRange(dataPoints);

            simulatorService.InitializeAsync(configuration).Wait();
            simulatorService.DataPointChanged += OnDataPointChanged;

            _iedSimulators.Add(simulatorService);
        }

        public List<string> GetAvailableControls()
        {
            var controls = new List<string>();
            
            foreach (var simulator in _iedSimulators)
            {
                if (simulator.Configuration == null) continue;

                var controlPoints = simulator.Configuration.DataPoints
                    .Where(dp => 
                        // Filter titik kontrol berdasarkan metadata
                        dp.Metadata.TryGetValue("LNClass", out var lnClass) && 
                        (lnClass == "CSWI" || lnClass == "XCBR")
                    )
                    .Select(dp => new 
                    { 
                        Id = dp.Id, 
                        Name = dp.Name,
                        LNClass = dp.Metadata["LNClass"],
                        ControlModel = dp.Metadata["ControlModel"],
                        DoName = dp.Metadata["DoName"]
                    });
                
                foreach (var point in controlPoints)
                {
                    controls.Add($"{point.Id} ({point.Name}) - {point.LNClass} [{point.ControlModel}]");
                }
            }

            return controls;
        }

        private string ExtractStationName(XDocument xmlDoc, XNamespace scl)
        {
            // Coba ambil nama dari Header
            var headerName = xmlDoc.Descendants(scl + "Header")
                .FirstOrDefault()?.Attribute("id")?.Value;

            // Jika tidak ada, coba dari IED
            var iedName = xmlDoc.Descendants(scl + "IED")
                .FirstOrDefault()?.Attribute("name")?.Value;

            // Fallback ke nama file tanpa ekstensi
            return headerName ?? iedName ?? Path.GetFileNameWithoutExtension(xmlDoc.BaseUri);
        }

        private List<DataPoint> DiscoverDataPointsFromIcdFile(XDocument xmlDoc, XNamespace scl, string stationName)
        {
            var dataPoints = new List<DataPoint>();

            // Cari elemen LN (Logical Node) untuk kontrol
            var lnElements = xmlDoc.Descendants(scl + "LN")
                .Where(ln => 
                    ln.Attribute("lnClass")?.Value == "CSWI" || 
                    ln.Attribute("lnClass")?.Value == "XCBR"
                );

            foreach (var lnElement in lnElements)
            {
                string lnClass = lnElement.Attribute("lnClass")?.Value ?? "";
                string lnInst = lnElement.Attribute("lnInst")?.Value ?? "";
                string ldInst = lnElement.Parent?.Parent?.Attribute("inst")?.Value ?? "";

                // Cari model kontrol
                var ctlModelElement = lnElement.Descendants(scl + "DAI")
                    .FirstOrDefault(dai => dai.Attribute("name")?.Value == "ctlModel");
                
                string ctlModel = ctlModelElement?.Element(scl + "Val")?.Value ?? "status-only";

                // Definisi titik kontrol berdasarkan jenis IED
                var controlPoints = lnClass switch
                {
                    "CSWI" => new[]
                    {
                        new { DoName = "Pos", Name = "Posisi Saklar" },
                        new { DoName = "Loc", Name = "Status Lokal" }
                    },
                    "XCBR" => new[]
                    {
                        new { DoName = "Pos", Name = "Posisi Pemutus" },
                        new { DoName = "BlkOpn", Name = "Blok Buka" },
                        new { DoName = "BlkCls", Name = "Blok Tutup" }
                    },
                    _ => Array.Empty<dynamic>()
                };

                foreach (var point in controlPoints)
                {
                    // Tambahkan berbagai atribut untuk setiap titik kontrol
                    string[] attributes = { "stVal", "q", "t" };
                    
                    foreach (var attr in attributes)
                    {
                        string id = $"{stationName}/{ldInst}/{lnClass}{lnInst}.{point.DoName}.{attr}$ST";
                        string name = $"{lnClass} {point.Name} {attr}";

                        dataPoints.Add(new DataPoint
                        {
                            Id = id,
                            Name = name,
                            Type = DetermineControlPointType(attr),
                            Metadata = new Dictionary<string, string>
                            {
                                { "LNClass", lnClass },
                                { "ControlModel", ctlModel },
                                { "DoName", point.DoName },
                                { "Attribute", attr }
                            }
                        });
                    }
                }
            }

            return dataPoints;
        }

        private string DetermineControlPointType(string attribute)
        {
            return attribute switch
            {
                "stVal" => "int",     // Nilai status biasanya integer
                "q" => "string",       // Kualitas biasanya string/enum
                "t" => "string",       // Timestamp biasanya string
                _ => "string"
            };
        }

        private string DetermineDataType(XDocument xmlDoc, XNamespace scl, string doName, string daName)
        {
            // Cari definisi tipe data di DataTypeTemplates
            var doType = xmlDoc.Descendants(scl + "DOType")
                .FirstOrDefault(dt => 
                    dt.Descendants(scl + "DA")
                    .Any(da => 
                        da.Attribute("name")?.Value == daName
                    )
                );

            if (doType != null)
            {
                var daElement = doType.Descendants(scl + "DA")
                    .FirstOrDefault(da => da.Attribute("name")?.Value == daName);

                if (daElement != null)
                {
                    string bType = daElement.Attribute("bType")?.Value;
                    return bType switch
                    {
                        "FLOAT32" => "float",
                        "INT32" => "int",
                        "BOOLEAN" => "bool",
                        _ => "string"
                    };
                }
            }

            return "float"; // Default
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
            if (!IsLoggingEnabled) return;

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
            _updateCounters.AddOrUpdate(stationName, 1, (key, oldValue) => oldValue + 1);
        }
    }
}