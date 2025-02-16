using System.Collections.Generic;

namespace IEDSimulator.Core.Models
{
    public class IedConfiguration
    {
        public string StationName { get; set; }
        public string DeviceName { get; set; }
        public string IcdFilePath { get; set; }
        public int Port { get; set; }
        public List<DataPoint> DataPoints { get; set; } = new List<DataPoint>();
    }
}
