using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonMesh
{
    /// <summary>
    /// Message object, exchanged between peers in the mesh network.
    /// </summary>
    internal class Message
    {
        #region Internal-Members
         
        internal string Id { get; set; } 
        internal bool IsBroadcast { get; set; }
        internal bool SyncRequest { get; set; } 
        internal bool SyncResponse { get; set; } 
        internal int TimeoutMs { get; set; } 
        internal string SourceIpPort { get; set; } 
        internal string DestinationIpPort { get; set; } 
        internal MessageType Type { get; set; } 
        internal Dictionary<object, object> Metadata 
        { 
            get
            {
                return _Metadata;
            }
            set
            {
                if (value == null) _Metadata = new Dictionary<object, object>();
                else _Metadata = value;
            }
        }
         
        internal long ContentLength { get; set; } 
        internal Stream DataStream { get; set; } 
        internal byte[] Data
        {
            get
            { 
                if (_Data != null) return _Data;
                if (ContentLength <= 0) return null;
                _Data = Common.StreamToBytes(DataStream);
                return _Data;
            }
        }

        #endregion

        #region Private-Members

        private ISerializationHelper _Serializer = new DefaultSerializationHelper();
        private Dictionary<object, object> _Metadata = new Dictionary<object, object>();
        private byte[] _Data = null;

        #endregion

        #region Constructors-and-Factories

        private Message()
        {

        }
         
        internal Message(
            ISerializationHelper serializer,
            string sourceIpPort, 
            string destIpPort, 
            int? timeoutMs, 
            bool isBroadcast, 
            bool syncRequest, 
            bool syncResponse, 
            MessageType msgType, 
            Dictionary<object, object> metadata, 
            long contentLength, 
            Stream stream)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (String.IsNullOrEmpty(sourceIpPort)) throw new ArgumentNullException(nameof(sourceIpPort)); 
            if (String.IsNullOrEmpty(destIpPort)) throw new ArgumentNullException(nameof(destIpPort)); 
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            _Serializer = serializer;
            Id = Guid.NewGuid().ToString();
            IsBroadcast = isBroadcast;
            SyncRequest = syncRequest;
            SyncResponse = syncResponse;
            TimeoutMs = 0;

            if (syncRequest || syncResponse)
            {
                if (timeoutMs == null) throw new ArgumentNullException(nameof(timeoutMs));
                TimeoutMs = Convert.ToInt32(timeoutMs);
            }

            SourceIpPort = sourceIpPort;
            DestinationIpPort = destIpPort;
            Type = msgType;

            Metadata = metadata;
            ContentLength = contentLength; 
            DataStream = stream;
            DataStream.Seek(0, SeekOrigin.Begin);
        }
         
        internal Message(Stream stream, int bufferLen)
        {
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
            if (bufferLen < 1) throw new ArgumentException("Buffer length must be greater than zero.");

            string header = "";  
            int bytesRead = 0;
            byte[] buffer = new byte[1];

            while (true)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                header += Convert.ToChar(buffer[0]);
                if (header.EndsWith("\r\n\r\n"))
                {
                    break;
                }
            }
             
            string[] headerVals = header.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
             
            foreach (string curr in headerVals)
            {
                if (!String.IsNullOrEmpty(curr)) curr.TrimEnd(Environment.NewLine.ToCharArray());
                if (!String.IsNullOrEmpty(curr)) curr.Trim();
                if (String.IsNullOrEmpty(curr)) continue;

                string[] currHeader = curr.Split(':');

                string key = "";
                string val = "";

                if (currHeader.Length >= 2)
                {
                    key = currHeader[0];
                    val = currHeader[1].TrimStart().TrimEnd();

                    // catch anything with ':' in the value, for instance, IPv6 addresses
                    if (currHeader.Length > 2)
                    {
                        for (int i = 2; i < currHeader.Length; i++)
                        {
                            val += ":" + currHeader[i];
                        }
                    }

                    switch (key)
                    {
                        case "Id":
                            Id = val;
                            break;
                        case "IsBroadcast":
                            IsBroadcast = Convert.ToBoolean(val);
                            break;
                        case "SyncRequest":
                            SyncRequest = Convert.ToBoolean(val);
                            break;
                        case "SyncResponse":
                            SyncResponse = Convert.ToBoolean(val);
                            break;
                        case "TimeoutMs":
                            TimeoutMs = Convert.ToInt32(val);
                            break;
                        case "SourceIpPort":
                            SourceIpPort = val;
                            break; 
                        case "DestinationIpPort":
                            DestinationIpPort = val;
                            break;
                        case "Type":
                            Type = (MessageType)(Enum.Parse(typeof(MessageType), val));
                            break;
                        case "Metadata":
                            Metadata = _Serializer.DeserializeJson<Dictionary<object, object>>(val);
                            break;
                        case "ContentLength":
                            ContentLength = Convert.ToInt64(val);
                            break;

                        default:
                            throw new ArgumentException("Unknown header in message: " + key);
                    }
                }
            }
             
            DataStream = new MemoryStream();
            long bytesRemaining = ContentLength;
            buffer = new byte[bufferLen];
            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    DataStream.Write(buffer, 0, bytesRead);
                    bytesRemaining -= bytesRead;
                }
            }

            DataStream.Seek(0, SeekOrigin.Begin); 
        }

        #endregion

        #region Internal-Methods
         
        internal byte[] ToHeaderBytes()
        { 
            string header = "";

            if (!String.IsNullOrEmpty(Id)) header += "Id: " + Id + Environment.NewLine;
            header += "IsBroadcast: " + IsBroadcast + Environment.NewLine;
            header += "SyncRequest: " + SyncRequest + Environment.NewLine;
            header += "SyncResponse: " + SyncResponse + Environment.NewLine;
            header += "TimeoutMs: " + TimeoutMs.ToString() + Environment.NewLine;
            header += "SourceIpPort: " + SourceIpPort + Environment.NewLine;
            header += "DestinationIpPort: " + DestinationIpPort + Environment.NewLine;
            header += "Type: " + Type.ToString() + Environment.NewLine;
            header += "Metadata: " + _Serializer.SerializeJson(Metadata, false) + Environment.NewLine;
            header += "ContentLength: " + ContentLength.ToString() + Environment.NewLine;
            header += Environment.NewLine;

            return Encoding.UTF8.GetBytes(header); 
        }
         
        #endregion

        #region Private-Methods

        #endregion
    }
}
