namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime;
    using System.Text.Json;
    using WatsonTcp;

    /// <summary>
    /// Event arguments passed when a message is received.
    /// </summary>
    public class MeshMessageReceivedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Unique ID for the message. 
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Indicates if the message is a broadcast.
        /// </summary>
        public bool IsBroadcast { get; set; } = false;

        /// <summary>
        /// Indicates if the message is a synchronous message request.
        /// </summary>
        public bool SyncRequest { get; set; } = false;

        /// <summary>
        /// Indicates if the message is a response to a synchronous message request.
        /// </summary>
        public bool SyncResponse { get; set; } = false;

        /// <summary>
        /// For synchronous requests or responses, the number of milliseconds before the message expires.
        /// </summary>
        public int TimeoutMs { get; set; } = 0;

        /// <summary>
        /// The sender's server IP:port.
        /// </summary>
        public string SourceIpPort { get; set; } = null;

        /// <summary>
        /// The sender's GUID.
        /// </summary>
        public Guid SourceGuid { get; set; } = default(Guid);

        /// <summary>
        /// The receiver's server IP:port.
        /// </summary>
        public string DestinationIpPort { get; set; } = null;

        /// <summary>
        /// The receiver's GUID.
        /// </summary>
        public Guid DestinationGuid { get; set; } = default(Guid);

        /// <summary>
        /// The type of message being sent.
        /// </summary>
        public MessageTypeEnum Type { get; set; } = MessageTypeEnum.Data;

        /// <summary>
        /// Dictionary containing metadata to include with the message.
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
        /// Data.
        /// </summary>
        public byte[] Data { get; set; } = null;

        #endregion

        #region Private-Members

        private Dictionary<string, object> _Metadata = new Dictionary<string, object>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="args">Watson TCP message received event arguments.</param>
        /// <param name="localIpPort">Local IP port.</param>
        /// <param name="localGuid">Local GUID.</param>
        public MeshMessageReceivedEventArgs(WatsonTcp.MessageReceivedEventArgs args, string localIpPort, Guid localGuid)
        {
            try
            {
                if (args == null) throw new ArgumentNullException(nameof(args));

                SourceIpPort = args.Client.IpPort;
                SourceGuid = args.Client.Guid;
                DestinationIpPort = localIpPort;
                DestinationGuid = localGuid;
                Metadata = new Dictionary<string, object>();
                
                if (args.Metadata != null && args.Metadata.Count > 0)
                {
                    foreach (KeyValuePair<string, object> kvp in args.Metadata)
                    {
                        if (kvp.Key.Equals("X-Id"))
                        {
                            Id = kvp.Value.ToString();
                        }
                        else if (kvp.Key.Equals("X-IsBroadcast"))
                        {
                            IsBroadcast = bool.Parse(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-SyncRequest"))
                        {
                            SyncRequest = bool.Parse(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-SyncResponse"))
                        {
                            SyncResponse = bool.Parse(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-TimeoutMs"))
                        {
                            TimeoutMs = Convert.ToInt32(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-SourceIpPort"))
                        {
                            SourceIpPort = kvp.Value.ToString();
                        }
                        else if (kvp.Key.Equals("X-SourceGuid"))
                        {
                            SourceGuid = Guid.Parse(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-DestinationIpPort"))
                        {
                            DestinationIpPort = kvp.Value.ToString();
                        }
                        else if (kvp.Key.Equals("X-DestinationGuid"))
                        {
                            DestinationGuid = Guid.Parse(kvp.Value.ToString());
                        }
                        else if (kvp.Key.Equals("X-Type"))
                        {
                            Type = (MessageTypeEnum)(Enum.Parse(typeof(MessageTypeEnum), kvp.Value.ToString()));
                        }
                        else
                        {
                            Metadata.Add(kvp.Key, kvp.Value);
                        }
                    }
                }

                Data = args.Data;
            }
            catch (Exception e)
            {
                Console.WriteLine(SerializationHelper.SerializeJson(e, true));
            }
        }

        internal MeshMessageReceivedEventArgs(Message msg)
        {
            Id = msg.Id;
            SyncRequest = msg.SyncRequest;
            SyncResponse = msg.SyncResponse;
            TimeoutMs = msg.TimeoutMs;
            SourceIpPort = msg.SourceIpPort;
            SourceGuid = msg.SourceGuid;
            DestinationIpPort = msg.DestinationIpPort;
            DestinationGuid = msg.DestinationGuid;
            Type = msg.Type;
            Metadata = msg.Metadata;
            Data = msg.Data;
        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
