using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    /// <summary>
    /// Message object, exchanged between peers in the mesh network.
    /// </summary>
    public class Message
    {
        #region Public-Members

        /// <summary>
        /// Unique ID for the message. 
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Indicates if the message is a synchronous message request.
        /// </summary>
        public bool SyncRequest { get; set; }

        /// <summary>
        /// Indicates if the message is a response to a synchronous message request.
        /// </summary>
        public bool SyncResponse { get; set; }

        /// <summary>
        /// For synchronous requests or responses, the number of milliseconds before the message expires.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// The sender's server IP.
        /// </summary>
        public string SourceIp { get; set; }

        /// <summary>
        /// The sender's server port.
        /// </summary>
        public int SourcePort { get; set; }

        /// <summary>
        /// The receiver's server IP.
        /// </summary>
        public string DestinationIp { get; set; }

        /// <summary>
        /// The receiver's server port.
        /// </summary>
        public int DestinationPort { get; set; }

        /// <summary>
        /// The type of message being sent.
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// The data being transmitted.
        /// </summary>
        public byte[] Data { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// DO NOT USE.  Use the more specific constructor.
        /// </summary>
        public Message()
        {

        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="sourceIp">The sender's server IP.</param>
        /// <param name="sourcePort">The sender's server port.</param>
        /// <param name="recipientIp">The recipient's server IP.</param>
        /// <param name="recipientPort">The recipient's server port.</param>
        /// <param name="timeoutMs">For synchronous requests or responses, the number of milliseconds before the message expires.</param>
        /// <param name="syncRequest">Indicates if the message is a synchronous message request.</param>
        /// <param name="syncResponse">Indicates if the message is a response to a synchronous message request.</param>
        /// <param name="msgType">The type of message being sent.</param>
        /// <param name="data">The data being transmitted.</param>
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

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
