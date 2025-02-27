using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;
using IEDSimulator.Core.Enums;

namespace IEDSimulator.Core.Interfaces
{
    public interface IDataPointRepository
    {
        Task<DataPoint> GetDataPointAsync(string id);
        Task<IEnumerable<DataPoint>> GetAllDataPointsAsync();
        Task UpdateDataPointAsync(DataPoint dataPoint);
        Task AddDataPointAsync(DataPoint dataPoint);

        // Tambahkan metode kontrol baru
        Task<bool> ExecuteControlAsync(string controlId, ControlOperation operation);
    }
}
