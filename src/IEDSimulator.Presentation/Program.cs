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

            // Inisialisasi simulator multi-IED
            var multiIedSimulator = new MultiIedSimulator();

            // Tambahkan setiap IED ke simulator
            foreach (var icdFile in icdFiles)
            {
                multiIedSimulator.AddIedSimulator(icdFile);
            }

            // Jalankan simulasi di background
            _ = multiIedSimulator.StartSimulationAsync();

            // Tampilkan menu utama dan tunggu interaksi
            await ShowMainMenu(multiIedSimulator);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Kesalahan pada simulator multi-IED: {ex.Message}");
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