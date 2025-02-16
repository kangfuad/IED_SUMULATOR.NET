using System;
using System.Threading.Tasks;
using IEDSimulator.Core.Enums;
using IEDSimulator.Presentation;
using System.Linq;

namespace IEDSimulator.Presentation
{
    public class InteractiveControlMenu
    {
        private readonly MultiIedSimulator _multiIedSimulator;

        public InteractiveControlMenu(MultiIedSimulator multiIedSimulator)
        {
            _multiIedSimulator = multiIedSimulator;
        }

        public async Task ShowMenu()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("=== Menu Kontrol IED ===");
                Console.WriteLine("1. Lihat Daftar Kontrol");
                Console.WriteLine("2. Eksekusi Kontrol");
                Console.WriteLine("3. Kembali ke Menu Utama");
                Console.Write("Pilih opsi: ");

                var key = Console.ReadKey().KeyChar;
                Console.WriteLine();

                switch (key)
                {
                    case '1':
                        await ListAvailableControls();
                        break;
                    case '2':
                        await ExecuteControlMenu();
                        break;
                    case '3':
                        return;
                    default:
                        Console.WriteLine("Pilihan tidak valid.");
                        break;
                }

                Console.WriteLine("\nTekan tombol apa pun untuk melanjutkan...");
                Console.ReadKey();
            }
        }

        private Task ListAvailableControls()
        {
            Console.Clear();
            Console.WriteLine("=== Daftar Kontrol Tersedia ===");
            var controls = _multiIedSimulator.GetAvailableControls();

            if (controls.Count == 0)
            {
                Console.WriteLine("Tidak ada kontrol yang tersedia.");
                return Task.CompletedTask;
            }

            for (int i = 0; i < controls.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {controls[i]}");
            }

            return Task.CompletedTask;
        }

        private async Task ExecuteControlMenu()
        {
            var controls = _multiIedSimulator.GetAvailableControls();

            if (controls.Count == 0)
            {
                Console.WriteLine("Tidak ada kontrol yang tersedia.");
                return;
            }

            Console.Clear();
            Console.WriteLine("=== Eksekusi Kontrol ===");
            Console.WriteLine("Pilih kontrol:");

            for (int i = 0; i < controls.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {controls[i]}");
            }
            Console.WriteLine("0. Kembali");

            Console.Write("Masukkan nomor kontrol: ");
            if (!int.TryParse(Console.ReadLine(), out int selectedIndex) || 
                selectedIndex < 0 || 
                selectedIndex > controls.Count)
            {
                Console.WriteLine("Pilihan tidak valid.");
                return;
            }

            if (selectedIndex == 0) return;

            var selectedControl = controls[selectedIndex - 1];
            var fullControlId = selectedControl.Split('(', ')')[1].Trim();

            // Ekstrak nama stasiun dari kontrol
            var stationName = fullControlId.Split('/')[0];
            var simulator = _multiIedSimulator.GetSimulatorByStationName(stationName);

            if (simulator != null)
            {
                var controlPoint = simulator.Configuration.DataPoints
                    .FirstOrDefault(dp => dp.Id.Contains(fullControlId));

                if (controlPoint != null)
                {
                    Console.WriteLine($"\nDetail Kontrol:");
                    foreach (var metadata in controlPoint.Metadata)
                    {
                        Console.WriteLine($"{metadata.Key}: {metadata.Value}");
                    }
                }

                // Pilih operasi kontrol
                Console.WriteLine("\nPilih Operasi:");
                Console.WriteLine("1. Open");
                Console.WriteLine("2. Close");
                Console.WriteLine("3. Select");
                Console.WriteLine("4. Cancel");
                Console.WriteLine("0. Kembali");

                Console.Write("Masukkan pilihan operasi: ");
                if (!int.TryParse(Console.ReadLine(), out int operationIndex) || 
                    operationIndex < 0 || 
                    operationIndex > 4)
                {
                    Console.WriteLine("Pilihan tidak valid.");
                    return;
                }

                if (operationIndex == 0) return;

                ControlOperation operation = operationIndex switch
                {
                    1 => ControlOperation.Open,
                    2 => ControlOperation.Close,
                    3 => ControlOperation.Select,
                    4 => ControlOperation.Cancel,
                    _ => throw new ArgumentException("Operasi tidak valid")
                };

                bool result = await simulator.ExecuteControlAsync(fullControlId, operation);
                Console.WriteLine(result 
                    ? $"Kontrol berhasil dieksekusi: {fullControlId} - Operasi {operation}" 
                    : "Gagal mengeksekusi kontrol");
            }
            else
            {
                Console.WriteLine("Simulator tidak ditemukan.");
            }
        }
    }
}