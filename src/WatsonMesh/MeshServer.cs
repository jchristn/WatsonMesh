using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonMesh
{ 
    internal class MeshServer : IDisposable
    {
        #region Internal-Members

        internal event EventHandler<ClientConnectionEventArgs> ClientConnected;
        internal event EventHandler<ClientConnectionEventArgs> ClientDisconnected;
        internal event EventHandler<StreamReceivedEventArgs> MessageReceived;
        internal Action<string> Logger = null;

        #endregion

        #region Private-Members

        private bool _Disposed = false;
        private MeshSettings _Settings;
        private string _Ip = null;
        private int _Port = -1;
        private string _IpPort = null;
        private bool _Ssl = false;
        private string _PfxCertificateFile = null;
        private string _PfxCertificatePassword = null;
        private WatsonTcpServer _TcpServer;

        #endregion

        #region Constructors-and-Factories

        internal MeshServer(MeshSettings settings, string ip, int port, bool ssl, string pfxCertFile, string pfxCertPass)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _Settings = settings;
            _Ip = ip;
            _Port = port;
            _IpPort = ip + ":" + port;
            _Ssl = ssl;
            _PfxCertificateFile = pfxCertFile;
            _PfxCertificatePassword = pfxCertPass;

            List<string> localIpAddresses = GetLocalIpAddresses();
            if (_Ip.Equals("127.0.0.1"))
            {
                Logger?.Invoke("[MeshServer] Loopback IP address detected; only connections from local machine will be accepted");
            }
            else
            {
                if (!localIpAddresses.Contains(_Ip))
                {
                    Logger?.Invoke("[MeshServer] Specified IP address '" + _Ip + "' not found in local IP address list:");
                    foreach (string curr in localIpAddresses) Logger?.Invoke("  " + curr);
                    throw new ArgumentException("IP address must either be 127.0.0.1 or the IP address of a local network interface.");
                }
            }

            if (_Ssl)
            {
                Logger?.Invoke("[MeshServer] Initialized TCP server with SSL on IP:port " + _IpPort);
            }
            else
            {
                Logger?.Invoke("[MeshServer] Initialized TCP server on IP:port " + _IpPort);
            }
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

        #endregion

        #region Internal-Methods

        internal void Start()
        {
            if (_Ssl)
            {
                _TcpServer = new WatsonTcpServer(
                    _Ip,
                    _Port,
                    _PfxCertificateFile,
                    _PfxCertificatePassword);

                Logger?.Invoke("[MeshServer] Starting TCP server with SSL on IP:port " + _IpPort);
            }
            else
            {
                _TcpServer = new WatsonTcpServer(
                    _Ip,
                    _Port);

                Logger?.Invoke("[MeshServer] Starting TCP server on IP:port " + _IpPort);
            }

            _TcpServer.Settings.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates;
            _TcpServer.Settings.MutuallyAuthenticate = _Settings.MutuallyAuthenticate;
            _TcpServer.Settings.PresharedKey = _Settings.PresharedKey;
            _TcpServer.Settings.StreamBufferSize = _Settings.StreamBufferSize;

            _TcpServer.Events.ClientConnected += MeshServerClientConnected;
            _TcpServer.Events.ClientDisconnected += MeshServerClientDisconnected;
            _TcpServer.Events.StreamReceived += MeshServerStreamReceived;

            _TcpServer.Start();

            Logger?.Invoke("[MeshServer] Server started");
        }

        internal void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            Logger?.Invoke("[MeshServer] Disconnecting client " + ipPort);
            _TcpServer.DisconnectClient(ipPort);
        }

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

        #endregion

        #region Private-Methods

        private void MeshServerClientConnected(object sender, ConnectionEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Client " + args.Client.IpPort + " connected");
            ClientConnected?.Invoke(this, new ClientConnectionEventArgs(args.Client.IpPort));
        }

        private void MeshServerClientDisconnected(object sender, DisconnectionEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Client " + args.Client.IpPort + " disconnected");
            ClientDisconnected?.Invoke(this, new ClientConnectionEventArgs(args.Client.IpPort));
        }

        private void MeshServerStreamReceived(object sender, StreamReceivedEventArgs args)
        {
            Logger?.Invoke("[MeshServer] Message received from client " + args.Client.IpPort + ": " + args.ContentLength + " bytes");
            MessageReceived?.Invoke(this, args);
        }

        private List<string> GetLocalIpAddresses()
        {
            List<string> ret = new List<string>();
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    ret.Add(ip.ToString());
                }
            }
            return ret;
        }

        #endregion
    }
}
