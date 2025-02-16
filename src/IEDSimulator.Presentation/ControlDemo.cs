using System;
using System.Threading.Tasks;
using IEDSimulator.Presentation;
using IEC61850.Client;
using IEC61850.Common;
using IEDSimulator.Infrastructure.Repositories;
using IEDSimulator.Core.Enums;

namespace IEDSimulator.Presentation
{
    public class ControlDemo
    {
        private readonly MultiIedSimulator _multiIedSimulator;

        public ControlDemo(MultiIedSimulator multiIedSimulator)
        {
            _multiIedSimulator = multiIedSimulator;
        }

        public async Task RunDemoControlOperations()
        {
            // Contoh operasi kontrol pada berbagai IED
            await PerformControlDemo("CSWI_IED/CSWI_Control/CSWI1.Pos$ST", "CSWI");
            await PerformControlDemo("XCBR_IED/XCBR_Control/XCBR1.Pos$ST", "XCBR");
        }

        private async Task PerformControlDemo(string controlId, string stationName)
        {
            Console.WriteLine($"Demo Kontrol untuk {stationName}:");
            try 
            {
                // Cari simulator untuk stasiun tertentu
                var simulator = _multiIedSimulator.GetSimulatorByStationName(stationName);
                if (simulator == null)
                {
                    Console.WriteLine($"Simulator untuk {stationName} tidak ditemukan.");
                    return;
                }

                // Urutan operasi kontrol
                Console.WriteLine("1. Select Control");
                await simulator.ExecuteControlAsync(
                    controlId, 
                    ControlOperation.Select
                );

                await Task.Delay(1000); // Simulasi waktu proses

                Console.WriteLine("2. Eksekusi Open");
                await simulator.ExecuteControlAsync(
                    controlId, 
                    ControlOperation.Open
                );

                await Task.Delay(2000); // Tunggu sejenak

                Console.WriteLine("3. Eksekusi Close");
                await simulator.ExecuteControlAsync(
                    controlId, 
                    ControlOperation.Close
                );

                await Task.Delay(1000);

                Console.WriteLine("4. Batalkan Kontrol");
                await simulator.ExecuteControlAsync(
                    controlId, 
                    ControlOperation.Cancel
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kesalahan pada demo kontrol {stationName}: {ex.Message}");
            }
        }
    }
}