using System;
using System.Threading.Tasks;
using System.Linq;
using IEDSimulator.Presentation;
using IEDSimulator.Core.Services;
using IEDSimulator.Infrastructure.Repositories;

class Program
{
    static async Task Main(string[] args)
    {
        try 
        {
            // Daftar file ICD/SCL
            var icdFiles = new[]
            {
                "../Models/model_cswi.icd",
                "../Models/model_mmxu.icd",
                "../Models/model_xcbr.icd"
            };

            Console.WriteLine("Starting IED Simulators...");
            var multiIedSimulator = new MultiIedSimulator();

            // Tambahkan setiap IED ke simulator
            foreach (var icdFile in icdFiles)
            {
                try 
                {
                    multiIedSimulator.AddIedSimulator(icdFile);
                    Console.WriteLine($"Started simulator for {icdFile}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start simulator for {icdFile}: {ex.Message}");
                }
            }

            // Validasi port
            var usedPorts = multiIedSimulator.GetUsedPorts();
            if (usedPorts.Count != usedPorts.Distinct().Count())
            {
                Console.WriteLine("Warning: Duplicate ports detected!");
            }

            // Log informasi port yang digunakan
            Console.WriteLine("\nActive IED Simulators:");
            foreach (var simulator in multiIedSimulator.GetSimulators())
            {
                if (simulator.Configuration != null)
                {
                    var port = usedPorts[multiIedSimulator.GetSimulators().ToList().IndexOf(simulator)];
                    Console.WriteLine($"- {simulator.Configuration.StationName} listening on port {port}");
                }
            }

            Console.WriteLine("\nWaiting for client connections...");

            // Beri waktu untuk server menginisialisasi
            await Task.Delay(1000);

            // Jalankan simulasi di background
            _ = multiIedSimulator.StartSimulationAsync();

            Console.WriteLine("\nSimulators are running. Press any key to show menu...");
            Console.ReadKey();
            Console.Clear();

            // Tampilkan menu utama dan tunggu interaksi
            await ShowMainMenu(multiIedSimulator);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error pada simulator multi-IED: {ex.Message}");
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }

    static async Task ShowMainMenu(MultiIedSimulator multiIedSimulator)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("=== Simulator IED ===");
            Console.WriteLine("1. Menu Kontrol IED");
            Console.WriteLine("2. Lihat Data Point");
            Console.WriteLine("3. Aktifkan Logging");
            Console.WriteLine("4. Nonaktifkan Logging");
            Console.WriteLine("5. Keluar");
            Console.Write("Pilih opsi: ");

            var key = Console.ReadKey().KeyChar;
            Console.WriteLine();

            switch (key)
            {
                case '1':
                    var controlMenu = new InteractiveControlMenu(multiIedSimulator);
                    await controlMenu.ShowMenu();
                    break;
                case '2':
                    await ShowDataPoints(multiIedSimulator);
                    break;
                case '3':
                    multiIedSimulator.EnableLogging();
                    Console.WriteLine("Logging diaktifkan.");
                    break;
                case '4':
                    multiIedSimulator.DisableLogging();
                    Console.WriteLine("Logging dinonaktifkan.");
                    break;
                case '5':
                    await multiIedSimulator.StopSimulationAsync();
                    return;
                default:
                    Console.WriteLine("Pilihan tidak valid.");
                    break;
            }

            Console.WriteLine("\nTekan tombol apa pun untuk melanjutkan...");
            Console.ReadKey();
        }
    }

    static Task ShowDataPoints(MultiIedSimulator multiIedSimulator)
    {
        Console.Clear();
        Console.WriteLine("=== Daftar Data Point ===");
        var controls = multiIedSimulator.GetAvailableControls();
        
        if (controls.Count == 0)
        {
            Console.WriteLine("Tidak ada data point yang tersedia.");
            return Task.CompletedTask;
        }

        foreach (var control in controls)
        {
            Console.WriteLine(control);
        }

        Console.WriteLine("\nTekan tombol apa pun untuk kembali...");
        Console.ReadKey();
        return Task.CompletedTask;
    }
}