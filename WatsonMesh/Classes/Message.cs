using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    /// <summary>
    /// Message object, exchanged between peers in the mesh network.
    /// </summary>
    internal class Message
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
        /// Content length of the data.
        /// </summary>
        public long ContentLength { get; set; }
         
        /// <summary>
        /// The stream containing the data being transmitted.
        /// </summary>
        public Stream Data { get; set; }

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
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream from which message data should be read.</param>
        public Message(string sourceIp, int sourcePort, string recipientIp, int recipientPort, int? timeoutMs, bool syncRequest, bool syncResponse, MessageType msgType, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(sourceIp)) throw new ArgumentNullException(nameof(sourceIp));
            if (sourcePort < 0) throw new ArgumentException("Source port must be zero or greater.");
            if (String.IsNullOrEmpty(recipientIp)) throw new ArgumentNullException(nameof(recipientIp));
            if (recipientPort < 0) throw new ArgumentException("Recipient port must be zero or greater.");
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

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
             
            ContentLength = contentLength; 
            Data = stream;
            Data.Seek(0, SeekOrigin.Begin);
        }
         
        /// <summary>
        /// Construct a message from a stream.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="bufferLen">Buffer size to use.</param>
        public Message(Stream stream, int bufferLen)
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
                        case "SyncRequest":
                            SyncRequest = Convert.ToBoolean(val);
                            break;
                        case "SyncResponse":
                            SyncResponse = Convert.ToBoolean(val);
                            break;
                        case "TimeoutMs":
                            TimeoutMs = Convert.ToInt32(val);
                            break;
                        case "SourceIp":
                            SourceIp = val;
                            break;
                        case "SourcePort":
                            SourcePort = Convert.ToInt32(val);
                            break;
                        case "DestinationIp":
                            DestinationIp = val;
                            break;
                        case "DestinationPort":
                            DestinationPort = Convert.ToInt32(val);
                            break;
                        case "Type":
                            Type = (MessageType)(Enum.Parse(typeof(MessageType), val));
                            break;
                        case "ContentLength":
                            ContentLength = Convert.ToInt64(val);
                            break;

                        default:
                            throw new ArgumentException("Unknown header in message: " + key);
                    }
                }
            }
             
            Data = new MemoryStream();

            long bytesRemaining = ContentLength;
            buffer = new byte[bufferLen];
            while (bytesRemaining > 0)
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    Data.Write(buffer, 0, bytesRead);
                    bytesRemaining -= bytesRead;
                }
            }

            Data.Seek(0, SeekOrigin.Begin);
        }

        #endregion

        #region Public-Methods
         
        /// <summary>
        /// Produce a byte array containing only the headers.
        /// </summary>
        /// <returns>Byte array.</returns>
        public byte[] ToHeaderBytes()
        { 
            string header = "";

            if (!String.IsNullOrEmpty(Id)) header += "Id: " + Id + Environment.NewLine;
            header += "SyncRequest: " + SyncRequest + Environment.NewLine;
            header += "SyncResponse: " + SyncResponse + Environment.NewLine;
            header += "TimeoutMs: " + TimeoutMs.ToString() + Environment.NewLine;
            header += "SourceIp: " + SourceIp + Environment.NewLine;
            header += "SourcePort: " + SourcePort.ToString() + Environment.NewLine;
            header += "DestinationIp: " + DestinationIp + Environment.NewLine;
            header += "DestinationPort: " + DestinationPort + Environment.NewLine;
            header += "Type: " + Type.ToString() + Environment.NewLine;
            header += "ContentLength: " + ContentLength.ToString() + Environment.NewLine;
            header += Environment.NewLine;

            return Encoding.UTF8.GetBytes(header); 
        }

        /// <summary>
        /// Create a human-readable representation of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret = "";
            ret += "---" + Environment.NewLine;
            ret += "  Id            : " + Id + Environment.NewLine;
            ret += "  SyncRequest   : " + SyncRequest + Environment.NewLine;
            ret += "  SyncResponse  : " + SyncResponse + Environment.NewLine;
            ret += "  TimeoutMs     : " + TimeoutMs + Environment.NewLine;
            ret += "  Src<>Dst      : " + SourceIp + ":" + SourcePort + "<>" + DestinationIp + ":" + DestinationPort + Environment.NewLine;
            ret += "  Type          : " + Type.ToString() + Environment.NewLine;
            ret += "  ContentLength : " + ContentLength + " bytes" + Environment.NewLine;

            ret += "  Stream        : ";
            if (Data != null) ret += "[present]" + Environment.NewLine;
            else ret += "[not present]" + Environment.NewLine;
             
            return ret;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
