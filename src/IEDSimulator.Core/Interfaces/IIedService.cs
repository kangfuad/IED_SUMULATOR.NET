using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IEDSimulator.Core.Models;

namespace IEDSimulator.Core.Interfaces
{
    public interface IIedService
    {
        Task InitializeAsync(IedConfiguration configuration);
        Task StartSimulationAsync();
        Task StopSimulationAsync();
        Task<IEnumerable<DataPoint>> GetCurrentDataPointsAsync();
        event EventHandler<DataPoint> DataPointChanged;
    }
}
