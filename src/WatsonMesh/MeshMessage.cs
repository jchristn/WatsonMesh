namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Message object, exchanged between peers in the mesh network.
    /// </summary>
    public class MeshMessage
    {
        #region Public-Members

        /// <summary>
        /// ID.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Flag to indicate if the message is a broadcast.
        /// </summary>
        public bool IsBroadcast { get; set; } = false;

        /// <summary>
        /// Flag to indicate if the message is a synchronous request.
        /// </summary>
        public bool SyncRequest { get; set; } = false;

        /// <summary>
        /// Flag to indicate if the message is a synchronous response.
        /// </summary>
        public bool SyncResponse { get; set; } = false;

        /// <summary>
        /// Timeout in ms.
        /// </summary>
        public int TimeoutMs { get; set; } = 0;

        /// <summary>
        /// Source IP:port.
        /// </summary>
        public string SourceIpPort { get; set; } = null;

        /// <summary>
        /// Source GUID.
        /// </summary>
        public Guid SourceGuid { get; set; } = default(Guid);

        /// <summary>
        /// Destination IP:port.
        /// </summary>
        public string DestinationIpPort { get; set; } = null;

        /// <summary>
        /// Destination GUID.
        /// </summary>
        public Guid DestinationGuid { get; set; } = default(Guid);

        /// <summary>
        /// Message type.
        /// </summary>
        public MessageTypeEnum Type { get; set; }

        /// <summary>
        /// User-specified metadata.
        /// </summary>
        public Dictionary<string, object> Metadata 
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

        /// <summary>
        /// Message data.
        /// </summary>
        public byte[] Data { get; set; } = null;

        /// <summary>
        /// Headers.
        /// </summary>
        public Dictionary<string, object> Headers
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

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="sourceGuid">Source GUID.</param>
        /// <param name="sourceIpPort">Source IP:port.</param>
        /// <param name="destGuid">Destination GUID.</param>
        /// <param name="destIpPort">Destination IP:port.</param>
        /// <param name="timeoutMs">Timeout in ms.</param>
        /// <param name="isBroadcast">Flag to indicate if message is broadcast.</param>
        /// <param name="syncRequest">Flag to indicate if message is a synchronous request.</param>
        /// <param name="syncResponse">Flag to indicate if message is a synchronous response.</param>
        /// <param name="msgType">Message type.</param>
        /// <param name="metadata">Metadata.</param>
        /// <param name="data">Data.</param>
        public MeshMessage(
            Guid sourceGuid,
            Guid destGuid,
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

            SourceGuid = sourceGuid;
            SourceIpPort = sourceIpPort;
            DestinationGuid = destGuid;
            DestinationIpPort = destIpPort;
            Type = msgType;

            Metadata = metadata;
            Data = data;
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="args">Event arguments.</param>
        public MeshMessage(MeshMessageReceivedEventArgs args)
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

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
