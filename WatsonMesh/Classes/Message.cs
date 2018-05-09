using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    public class Message
    {
        public string Id { get; set; }
        public bool SyncRequest { get; set; }
        public bool SyncResponse { get; set; }
        public int TimeoutMs { get; set; }
        public string SourceIp { get; set; }
        public int SourcePort { get; set; }
        public string DestinationIp { get; set; }
        public int DestinationPort { get; set; }
        public MessageType Type { get; set; }
        public byte[] Data { get; set; }
        
        public Message()
        {

        }

        public Message(string sourceIp, int sourcePort, string recipientIp, int recipientPort, int? timeoutMs, bool syncRequest, bool syncResponse, MessageType msgType, byte[] data)
        {
            if (String.IsNullOrEmpty(sourceIp)) throw new ArgumentNullException(nameof(sourceIp));
            if (sourcePort < 0) throw new ArgumentException("Source port must be zero or greater.");
            if (String.IsNullOrEmpty(recipientIp)) throw new ArgumentNullException(nameof(recipientIp));
            if (recipientPort < 0) throw new ArgumentException("Recipient port must be zero or greater.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            Id = Guid.NewGuid().ToString();
            SyncRequest = syncRequest;
            SyncResponse = syncResponse;
            TimeoutMs = 0;

            if (syncRequest || syncResponse)
            {
                if (timeoutMs == null) throw new ArgumentNullException(nameof(timeoutMs));
                TimeoutMs = Convert.ToInt32(timeoutMs);
            }

            SourceIp = sourceIp;
            SourcePort = sourcePort;
            DestinationIp = recipientIp;
            DestinationPort = recipientPort;
            Type = msgType;
            Data = data;
        }
    }
}
