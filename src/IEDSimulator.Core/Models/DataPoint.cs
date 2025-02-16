using System;
using System.Collections.Generic;

namespace IEDSimulator.Core.Models
{
    public class DataPoint
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public dynamic Value { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}
