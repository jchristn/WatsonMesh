using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    internal class Message
    {
        public string IpPort { get; set; }
        public MessageType Type { get; set; }
        public byte[] Data { get; set; }

        public Message(string ipPort, MessageType msgType, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            IpPort = ipPort;
            Type = msgType;
            Data = data;
        }
    }
}
