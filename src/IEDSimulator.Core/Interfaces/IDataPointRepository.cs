using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;

namespace IEDSimulator.Core.Interfaces
{
    public interface IDataPointRepository
    {
        Task<DataPoint> GetDataPointAsync(string id);
        Task<IEnumerable<DataPoint>> GetAllDataPointsAsync();
        Task UpdateDataPointAsync(DataPoint dataPoint);
        Task AddDataPointAsync(DataPoint dataPoint);
    }
}
