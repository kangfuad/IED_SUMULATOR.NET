using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Interfaces;
using IEC61850.Client;  // Sesuaikan dengan namespace dari library
using IEC61850.Common;

namespace IEDSimulator.Infrastructure.Repositories
{
    public class IEC61850DataPointRepository : IDataPointRepository
    {
        private readonly Dictionary<string, DataPoint> _dataPoints = new Dictionary<string, DataPoint>();
        private readonly IedConnection _connection;

        public IEC61850DataPointRepository(string icdFilePath)
        {
            if (string.IsNullOrEmpty(icdFilePath))
                throw new ArgumentNullException(nameof(icdFilePath));

            // Inisialisasi koneksi IED
            _connection = new IedConnection();
        }

        public async Task<DataPoint> GetDataPointAsync(string id)
        {
            if (_dataPoints.TryGetValue(id, out var dataPoint))
            {
                try 
                {
                    // Baca nilai terbaru dari IED
                    var parts = id.Split('$');
                    var path = parts[0];
                    var fc = (FunctionalConstraint)Enum.Parse(typeof(FunctionalConstraint), parts[1]);
                    
                    var mmsValue = _connection.ReadValue(path, fc);
                    dataPoint.Value = ConvertMmsValueToObject(mmsValue);
                    dataPoint.Timestamp = DateTime.UtcNow;
                }
                catch 
                {
                    // Tangani kesalahan pembacaan
                }
                
                return dataPoint;
            }
            return null;
        }

        public Task<IEnumerable<DataPoint>> GetAllDataPointsAsync() => 
            Task.FromResult(_dataPoints.Values.AsEnumerable());

        public Task UpdateDataPointAsync(DataPoint dataPoint)
        {
            if (_dataPoints.ContainsKey(dataPoint.Id))
            {
                _dataPoints[dataPoint.Id] = dataPoint;
                
                try 
                {
                    // Tulis nilai ke IED
                    var parts = dataPoint.Id.Split('$');
                    var path = parts[0];
                    var fc = (FunctionalConstraint)Enum.Parse(typeof(FunctionalConstraint), parts[1]);
                    
                    var mmsValue = ConvertObjectToMmsValue(dataPoint.Value);
                    _connection.WriteValue(path, fc, mmsValue);
                }
                catch 
                {
                    // Tangani kesalahan penulisan
                }
            }
            return Task.CompletedTask;
        }

        public Task AddDataPointAsync(DataPoint dataPoint)
        {
            if (!_dataPoints.ContainsKey(dataPoint.Id))
            {
                _dataPoints.Add(dataPoint.Id, dataPoint);
            }
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
    }
}