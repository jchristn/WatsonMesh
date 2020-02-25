using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonMesh
{ 
    internal class MeshServer : IDisposable
    {
        #region Public-Members
          
        internal event EventHandler<ClientConnectionEventArgs> ClientConnected; 
        internal event EventHandler<ClientConnectionEventArgs> ClientDisconnected; 
        internal event EventHandler<StreamReceivedFromClientEventArgs> MessageReceived; 
        internal Action<string> Logger = null;

        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private MeshSettings _Settings;
        private MeshPeer _Self;
        private WatsonTcpServer _TcpServer;

        #endregion

        #region Constructors-and-Factories
         
        internal MeshServer(MeshSettings settings, MeshPeer self)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (self == null) throw new ArgumentNullException(nameof(self));

            _Settings = settings;
            _Self = self;

            Logger?.Invoke("[MeshServer] Initialized on IP:port " + _Self.IpPort);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Logger?.Invoke("[MeshServer] Disposing");
            Dispose(true);
            GC.SuppressFinalize(this);
            Logger?.Invoke("[MeshServer] Disposed");
        }

        internal void Start()
        {
            string ip = null;
            int port = -1;
            Common.ParseIpPort(_Self.IpPort, out ip, out port);

            if (_Self.Ssl)
            {
                _TcpServer = new WatsonTcpServer(
                    ip, 
                    port,
                    _Self.PfxCertificateFile,
                    _Self.PfxCertificatePassword);

                Logger?.Invoke("[MeshServer] Initialized TCP server with SSL on IP:port " + _Self.IpPort);
            }
            else
            {
                _TcpServer = new WatsonTcpServer(
                    ip,
                    port);

                Logger?.Invoke("[MeshServer] Initialized TCP server on IP:port " + _Self.IpPort);
            }

            _TcpServer.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates; 
            _TcpServer.MutuallyAuthenticate = _Settings.MutuallyAuthenticate;
            _TcpServer.PresharedKey = _Settings.PresharedKey; 
            _TcpServer.StreamBufferSize = _Settings.StreamBufferSize;

            _TcpServer.ClientConnected += MeshServerClientConnected;
            _TcpServer.ClientDisconnected += MeshServerClientDisconnected;
            _TcpServer.StreamReceived += MeshServerStreamReceived;

            _TcpServer.Start();

            Logger?.Invoke("[MeshServer] Server started");
        }

        internal void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort)); 
            Logger?.Invoke("[MeshServer] Disconnecting client " + ipPort); 
            _TcpServer.DisconnectClient(ipPort);
        }

        #endregion

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed)
            {
                return;
            }

            if (disposing)
            {
                if (_TcpServer != null) _TcpServer.Dispose();
            }

            _Disposed = true;
        }

        private void MeshServerClientConnected(object sender, ClientConnectedEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Client " + args.IpPort + " connected");
            ClientConnected?.Invoke(this, new ClientConnectionEventArgs(args.IpPort));
        }

        private void MeshServerClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Client " + args.IpPort + " disconnected");
            ClientDisconnected?.Invoke(this, new ClientConnectionEventArgs(args.IpPort));
        }
         
        private void MeshServerStreamReceived(object sender, StreamReceivedFromClientEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Message received from client " + args.IpPort + ": " + args.ContentLength + " bytes");
            MessageReceived?.Invoke(this, args);
        }

        #endregion
    }
}
