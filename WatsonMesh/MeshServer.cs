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
        internal event EventHandler<ClientConnectionEventArgs> ClientConnected; 
        internal event EventHandler<ClientConnectionEventArgs> ClientDisconnected; 
        internal event EventHandler<StreamReceivedFromClientEventArgs> MessageReceived; 
        internal Action<string> Logger = null;
         
        private bool _Disposed = false;
        private MeshSettings _Settings;
        private string _Ip = null;
        private int _Port = -1;
        private string _IpPort = null;
        private bool _Ssl = false;
        private string _PfxCertificateFile = null;
        private string _PfxCertificatePassword = null;
        private WatsonTcpServer _TcpServer;
         
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
    }
}
