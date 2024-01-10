namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime;
    using WatsonTcp;

    /// <summary>
    /// Event arguments passed when a message is received.
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Unique ID for the message. 
        /// </summary>
        public string Id { get; set; } = null;

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
        /// Content length of the data.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// The stream containing the data being transmitted.
        /// </summary>
        public Stream DataStream { get; set; } = null;

        /// <summary>
        /// The data from DataStream.
        /// Using Data will fully read the contents of DataStream.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (_Data != null) return _Data;
                if (ContentLength <= 0) return null;
                _Data = Common.StreamToBytes(DataStream).Result;
                return _Data;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<string, object> _Metadata = new Dictionary<string, object>();
        private byte[] _Data = null;

        #endregion

        #region Constructors-and-Factories

        internal MessageReceivedEventArgs(StreamReceivedEventArgs args, string localIpPort, Guid localGuid)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            SourceIpPort = args.Client.IpPort;
            SourceGuid = args.Client.Guid;
            DestinationIpPort = localIpPort;
            DestinationGuid = localGuid;
            Metadata = args.Metadata;
            ContentLength = args.ContentLength;
            DataStream = args.DataStream;
        }

        internal MessageReceivedEventArgs(Message msg)
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
            ContentLength = msg.ContentLength;
            DataStream = msg.DataStream;
        }
         
        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
