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

        public List<int> GetUsedPorts()
        {
            return _iedSimulators
                .Where(simulator => simulator.Configuration != null)
                .Select(simulator => ExtractPortFromConfig(simulator.Configuration))
                .ToList();
        }

        // Metode untuk menonaktifkan logging
        public void DisableLogging()
        {
            IsLoggingEnabled = false;
        }

        public IEnumerable<IedSimulatorService> GetSimulators()
        {
            return _iedSimulators.ToList();
        }

        private int ExtractPortFromConfig(IedConfiguration config)
        {
            try
            {
                // Parse IP address dari file path untuk mendapatkan port
                // Format: CSWI_IED -> 102, MMXU_IED -> 103, XCBR_IED -> 104
                if (config.StationName.StartsWith("CSWI")) return 10102;
                if (config.StationName.StartsWith("MMXU")) return 10103;
                if (config.StationName.StartsWith("XCBR")) return 10104;
                
                // Default port jika tidak ada match
                return 10102 + _iedSimulators.Count;
            }
            catch
            {
                // Fallback ke default port + increment
                return 102 + _iedSimulators.Count;
            }
        }

        public IedSimulatorService GetSimulatorByStationName(string stationName)
        {
            if (string.IsNullOrEmpty(stationName))
            {
                throw new ArgumentNullException(nameof(stationName));
            }

            // Parse stationName dari full controlId
            string parsedStationName = stationName;
            if (stationName.Contains("/"))
            {
                parsedStationName = stationName.Split('/')[0];
            }

            var simulator = _iedSimulators.FirstOrDefault(
                sim => sim.Configuration != null && 
                    sim.Configuration.StationName != null &&
                    sim.Configuration.StationName.Equals(parsedStationName, StringComparison.OrdinalIgnoreCase)
            );

            return simulator ?? throw new InvalidOperationException($"No simulator found for station {stationName}");
        }

        public void AddIedSimulator(string icdFilePath)
        {
            // Parse XML
            XDocument xmlDoc = XDocument.Load(icdFilePath);
            XNamespace scl = "http://www.iec.ch/61850/2003/SCL";

            string stationName = ExtractStationName(xmlDoc, scl);
            int port = DeterminePort(stationName);

            Console.WriteLine($"Initializing {stationName} simulator on port {port}...");
            
            var dataPointRepository = new IEC61850DataPointRepository(icdFilePath, port);
            var simulatorService = new IedSimulatorService(dataPointRepository);

            var configuration = new IedConfiguration
            {
                StationName = stationName,
                DeviceName = $"Simulator_{stationName}",
                IcdFilePath = icdFilePath
            };

            simulatorService.InitializeAsync(configuration).Wait();
            simulatorService.DataPointChanged += OnDataPointChanged;

            _iedSimulators.Add(simulatorService);
            Console.WriteLine($"Successfully added {stationName} simulator on port {port}");
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

        private int ExtractPortFromSCL(XDocument xmlDoc, XNamespace scl)
        {
            try 
            {
                // Coba ambil port dari Communication section
                var address = xmlDoc.Descendants(scl + "Address").FirstOrDefault();
                if (address != null)
                {
                    var portElement = address.Elements(scl + "P")
                        .FirstOrDefault(p => p.Attribute("type")?.Value == "OSI-TSEL");
                    
                    if (portElement != null)
                    {
                        // Convert TSEL ke port number
                        // Biasanya dimulai dari 102 dan increment
                        string tsel = portElement.Value;
                        return 102 + Convert.ToInt32(tsel, 16);
                    }
                }
                
                // Jika tidak ada, gunakan port default + increment
                return 102 + _iedSimulators.Count;
            }
            catch
            {
                // Fallback ke port default + increment jika gagal
                return 102 + _iedSimulators.Count;
            }
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

        private int DeterminePort(string stationName)
        {
            // Menentukan port berdasarkan nama stasiun
            return stationName switch
            {
                var s when s.StartsWith("CSWI") => 10102,
                var s when s.StartsWith("MMXU") => 10103,
                var s when s.StartsWith("XCBR") => 10104,
                _ => 10102 // default port
            };
        }
            
    }
}