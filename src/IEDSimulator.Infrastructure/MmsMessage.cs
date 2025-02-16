using System;

namespace IEDSimulator.Infrastructure
{
    public class MmsMessage
    {
        public byte[] Header { get; set; }
        public byte[] Data { get; set; }
        
        public static byte[] CreateInitMessage()
        {
            // Basic MMS Initiate Request response
            return new byte[] { 
                0x03, 0x00, 0x00, 0x16, 0x11, 0xE0, 0x00, 0x00, 
                0x00, 0x01, 0x00, 0xA1, 0x07, 0x02, 0x01, 0x03,
                0xA2, 0x02, 0x80, 0x00
            };
        }
    }
}