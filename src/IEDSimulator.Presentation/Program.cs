using System;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Infrastructure.Repositories;
using IEDSimulator.Core.Services;
using IEC61850.Client;  // Tambahkan namespace ini
using IEC61850.Common;
using IEDSimulator.Presentation;

namespace IEDSimulator.Presentation
{
    class Program
    {

        static async Task Main(string[] args)
        {
            try 
            {
                // Daftar file ICD/SCL
                var icdFiles = new[]
                {
                    new { Path = "../Models/model_cswi.icd", StationName = "IED_CSWI" },
                    new { Path = "../Models/model_mmxu.icd", StationName = "IED_MMXU" },
                    new { Path = "../Models/model_xcbr.icd", StationName = "IED_XCBR" }
                };

                // Inisialisasi simulator multi-IED
                var multiIedSimulator = new MultiIedSimulator();

                // Tambahkan setiap IED ke simulator
                foreach (var icdFile in icdFiles)
                {
                    multiIedSimulator.AddIedSimulator(icdFile.Path, icdFile.StationName);
                }

                // Jalankan simulasi
                await multiIedSimulator.StartSimulationAsync();

                Console.WriteLine("Simulator Multi-IED berjalan. Tekan tombol apa pun untuk berhenti.");
                Console.ReadKey();

                // Hentikan simulasi
                await multiIedSimulator.StopSimulationAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Kesalahan pada simulator multi-IED: {ex.Message}");
            }
        }

        private static void OnDataPointChanged(object sender, Core.Models.DataPoint dataPoint)
        {
            Console.WriteLine(
                $"Perubahan Titik Data: " +
                $"ID: {dataPoint.Id}, " +
                $"Nama: {dataPoint.Name}, " +
                $"Nilai: {dataPoint.Value}, " +
                $"Waktu: {dataPoint.Timestamp}"
            );
        }
    }
}