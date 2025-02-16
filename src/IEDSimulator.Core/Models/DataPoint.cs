using System;

namespace IEDSimulator.Core.Models
{
    public class DataPoint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public dynamic Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
