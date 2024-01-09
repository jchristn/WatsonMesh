using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WatsonMesh
{
    /// <summary>
    /// Event arguments passed when a message is received.
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Unique ID for the message. 
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Indicates if the message is a broadcast.
        /// </summary>
        public bool IsBroadcast { get; }

        /// <summary>
        /// Indicates if the message is a synchronous message request.
        /// </summary>
        public bool SyncRequest { get; }

        /// <summary>
        /// Indicates if the message is a response to a synchronous message request.
        /// </summary>
        public bool SyncResponse { get; }

        /// <summary>
        /// For synchronous requests or responses, the number of milliseconds before the message expires.
        /// </summary>
        public int TimeoutMs { get; }

        /// <summary>
        /// The sender's server IP:port.
        /// </summary>
        public string SourceIpPort { get; }

        /// <summary>
        /// The receiver's server IP:port.
        /// </summary>
        public string DestinationIpPort { get; }

        /// <summary>
        /// The type of message being sent.
        /// </summary>
        public MessageType Type { get; }

        /// <summary>
        /// Dictionary containing metadata to include with the message.
        /// </summary>
        public Dictionary<object, object> Metadata
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

        /// <summary>
        /// Content length of the data.
        /// </summary>
        public long ContentLength { get; }

        /// <summary>
        /// The stream containing the data being transmitted.
        /// </summary>
        public Stream DataStream { get; }

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
                _Data = Common.StreamToBytes(DataStream);
                return _Data;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<object, object> _Metadata = new Dictionary<object, object>();
        private byte[] _Data = null;

        #endregion

        #region Constructors-and-Factories

        internal MessageReceivedEventArgs(Message msg)
        {
            Id = msg.Id;
            IsBroadcast = msg.IsBroadcast;
            SyncRequest = msg.SyncRequest;
            SyncResponse = msg.SyncResponse;
            TimeoutMs = msg.TimeoutMs;
            SourceIpPort = msg.SourceIpPort;
            DestinationIpPort = msg.DestinationIpPort;
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
