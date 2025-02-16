using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Interfaces;
using IEDSimulator.Core.Enums;
using IEC61850.Client;
using IEC61850.Common;

namespace IEDSimulator.Infrastructure.Repositories
{
    public class IEC61850DataPointRepository : IDataPointRepository
    {
        private readonly ConcurrentDictionary<string, DataPoint> _dataPoints = new ConcurrentDictionary<string, DataPoint>();
        private readonly IedConnection _connection;
        private readonly IedServer _server;
        private readonly int _port;
        private readonly string _icdFilePath;

        public IEC61850DataPointRepository(string icdFilePath, int defaultPort = 102)
        {
            _icdFilePath = icdFilePath ?? throw new ArgumentNullException(nameof(icdFilePath));
            _port = defaultPort;
            _connection = new IedConnection();
            _server = new IedServer(_port);
            InitializeServer();
        }

        private void InitializeServer()
        {
            try
            {
                _server.Start(); // Menggunakan Start() bukan StartAsync()
                Console.WriteLine($"Repository initialized on port {_port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize server: {ex.Message}");
                throw;
            }
        }

        public Task<DataPoint> GetDataPointAsync(string id)
        {
            return Task.FromResult(_dataPoints.TryGetValue(id, out var dataPoint) ? dataPoint : null);
        }

        public Task UpdateDataPointAsync(DataPoint dataPoint)
        {
            _dataPoints.AddOrUpdate(
                dataPoint.Id, 
                dataPoint, 
                (key, oldValue) => dataPoint
            );
            return Task.CompletedTask;
        }

        public Task AddDataPointAsync(DataPoint dataPoint)
        {
            _dataPoints.TryAdd(dataPoint.Id, dataPoint);
            return Task.CompletedTask;
        }

        private dynamic ConvertMmsValueToObject(MmsValue mmsValue)
        {
            // Konversi MmsValue ke tipe data dinamis
            switch (mmsValue.GetType())
            {
                case MmsType.MMS_BOOLEAN:
                    return mmsValue.GetBoolean();
                case MmsType.MMS_FLOAT:
                    return mmsValue.ToFloat();
                case MmsType.MMS_INTEGER:
                    return mmsValue.ToInt64();
                case MmsType.MMS_UNSIGNED:
                    return mmsValue.ToUint32();
                default:
                    return mmsValue.ToString();
            }
        }

        private MmsValue ConvertObjectToMmsValue(dynamic value)
        {
            // Konversi objek ke MmsValue
            if (value is bool boolVal)
                return new MmsValue(boolVal);
            if (value is float floatVal)
                return new MmsValue(floatVal);
            if (value is int intVal)
                return new MmsValue(intVal);
            if (value is uint uintVal)
                return new MmsValue(uintVal);
            
            return new MmsValue(value.ToString());
        }

        public async Task<bool> ExecuteControlAsync(string controlId, ControlOperation operation)
        {
            try 
            {
                // Pisahkan kontrolID menjadi komponen
                var parts = controlId.Split('$');
                var path = parts[0];
                var fc = (FunctionalConstraint)Enum.Parse(typeof(FunctionalConstraint), parts[1]);

                // Simulasi kontrol 
                switch (operation)
                {
                    case ControlOperation.Open:
                        Console.WriteLine($"Membuka kontrol: {controlId}");
                        break;
                    case ControlOperation.Close:
                        Console.WriteLine($"Menutup kontrol: {controlId}");
                        break;
                    case ControlOperation.Select:
                        Console.WriteLine($"Memilih kontrol: {controlId}");
                        break;
                    case ControlOperation.Cancel:
                        Console.WriteLine($"Membatalkan kontrol: {controlId}");
                        break;
                }

                // Tambahkan await untuk operasi asinkron
                var dataPoint = await GetDataPointAsync(controlId);
                
                if (dataPoint != null)
                {
                    dataPoint.Value = operation switch
                    {
                        ControlOperation.Open => 1,
                        ControlOperation.Close => 0,
                        _ => dataPoint.Value
                    };
                    dataPoint.Timestamp = DateTime.UtcNow;

                    await UpdateDataPointAsync(dataPoint);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kesalahan kontrol: {ex.Message}");
                return false;
            }
        }

        public Task<IEnumerable<DataPoint>> GetAllDataPointsAsync()
        {
            return Task.FromResult(_dataPoints.Values.ToList().AsEnumerable());
        }
    }
}