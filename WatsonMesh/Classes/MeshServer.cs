using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Watson
{
    /// <summary>
    /// Watson mesh networking server.
    /// </summary>
    internal class MeshServer
    {
        #region Public-Members
         
        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<string, Task> ClientConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<string, Task> ClientDisconnected = null;
         
        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<string, long, Stream, Task> MessageReceived = null;
         
        #endregion

        #region Private-Members
         
        private MeshSettings _Settings;
        private Peer _Self;
        private WatsonTcpServer _TcpServer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="self">Node details for the local node.</param>
        public MeshServer(MeshSettings settings, Peer self)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (self == null) throw new ArgumentNullException(nameof(self));

            _Settings = settings;
            _Self = self;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the Watson mesh server.
        /// </summary>
        public void Start()
        {
            if (_Self.Ssl)
            {
                _TcpServer = new WatsonTcpServer(
                    _Self.Ip,
                    _Self.Port,
                    _Self.PfxCertificateFile,
                    _Self.PfxCertificatePassword);
            }
            else
            {
                _TcpServer = new WatsonTcpServer(
                    _Self.Ip,
                    _Self.Port);
            }

            _TcpServer.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates; 
            _TcpServer.MutuallyAuthenticate = _Settings.MutuallyAuthenticate;
            _TcpServer.PresharedKey = _Settings.PresharedKey; 
            _TcpServer.ReadStreamBufferSize = _Settings.ReadStreamBufferSize;
            _TcpServer.ReadDataStream = false;
            _TcpServer.Debug = _Settings.Debug;

            _TcpServer.ClientConnected = MeshServerClientConnected;
            _TcpServer.ClientDisconnected = MeshServerClientDisconnected;
            _TcpServer.MessageReceived = null;
            _TcpServer.StreamReceived = MeshServerStreamReceived;

            _TcpServer.Start();
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="ipPort">IP address and port of the remoteclient, of the form IP:port.</param>
        public void DisconnectClient(string ipPort)
        {
            Console.WriteLine("DisconnectClient " + ipPort);
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            _TcpServer.DisconnectClient(ipPort);
        }

        #endregion

        #region Private-Methods

        private async Task MeshServerClientConnected(string ipPort)
        { 
            if (ClientConnected != null) await ClientConnected(ipPort);
        }

        private async Task MeshServerClientDisconnected(string ipPort)
        { 
            if (ClientDisconnected != null) await ClientDisconnected(ipPort);
        }
         
        private async Task MeshServerStreamReceived(string ipPort, long contentLength, Stream stream)
        { 
            if (MessageReceived != null) await MessageReceived(ipPort, contentLength, stream); 
        }

        #endregion
    }
}
