namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

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

        internal Guid SourceGuid { get; set; }
        internal string DestinationIpPort { get; set; } 

        internal Guid DestinationGuid { get; set; }
        internal MessageTypeEnum Type { get; set; } 
        internal Dictionary<string, object> Metadata 
        { 
            get
            {
                return _Metadata;
            }
            set
            {
                if (value == null) _Metadata = new Dictionary<string, object>();
                else _Metadata = value;
            }
        }
         
        internal byte[] Data { get; set; } = null;

        internal Dictionary<string, object> Headers
        {
            get
            {
                Dictionary<string, object> ret = new Dictionary<string, object>();
                ret.Add("X-Id", Id);
                ret.Add("X-IsBroadcast", IsBroadcast);
                ret.Add("X-SyncRequest", SyncRequest);
                ret.Add("X-SyncResponse", SyncResponse);
                ret.Add("X-TimeoutMs", TimeoutMs);
                ret.Add("X-SourceIpPort", SourceIpPort);
                ret.Add("X-SourceGuid", SourceGuid);
                ret.Add("X-DestinationIpPort", DestinationIpPort);
                ret.Add("X-DestinationGuid", DestinationGuid);
                ret.Add("X-Type", Type.ToString());
                return ret;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<string, object> _Metadata = new Dictionary<string, object>();

        #endregion

        #region Constructors-and-Factories

        internal Message(
            string sourceIpPort, 
            string destIpPort, 
            int? timeoutMs, 
            bool isBroadcast, 
            bool syncRequest, 
            bool syncResponse, 
            MessageTypeEnum msgType, 
            Dictionary<string, object> metadata,
            byte[] data)
        {
            if (String.IsNullOrEmpty(sourceIpPort)) throw new ArgumentNullException(nameof(sourceIpPort)); 
            if (String.IsNullOrEmpty(destIpPort)) throw new ArgumentNullException(nameof(destIpPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

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
            Data = data;
        }

        internal Message(MeshMessageReceivedEventArgs args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            if (args.Metadata != null && args.Metadata.Count > 0)
            {
                foreach (KeyValuePair<string, object> curr in args.Metadata)
                {
                    string key = curr.Key;
                    object val = curr.Value;

                    if (!String.IsNullOrEmpty(key)) key = curr.Key.Trim();

                    switch (key)
                    {
                        case "Id":
                            Id = val.ToString();
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
                            SourceIpPort = val.ToString();
                            break;
                        case "SourceGuid":
                            SourceGuid = Guid.Parse(val.ToString());
                            break;
                        case "DestinationIpPort":
                            DestinationIpPort = val.ToString();
                            break;
                        case "DestinationGuid":
                            DestinationGuid = Guid.Parse(val.ToString());
                            break;
                        case "Type":
                            Type = (MessageTypeEnum)(Enum.Parse(typeof(MessageTypeEnum), val.ToString()));
                            break;
                        case "Metadata":
                            Metadata = SerializationHelper.DeserializeJson<Dictionary<string, object>>(val.ToString());
                            break;

                        default:
                            throw new ArgumentException("Unknown header in message: " + key);
                    }
                }
            }

            Data = args.Data;
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
            header += "Metadata: " + SerializationHelper.SerializeJson(Metadata, false) + Environment.NewLine;
            header += Environment.NewLine;

            return Encoding.UTF8.GetBytes(header); 
        }
         
        #endregion

        #region Private-Methods

        #endregion
    }
}
