using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using WatsonTcp;

namespace WatsonMesh
{
    /// <summary>
    /// Watson mesh node.
    /// </summary>
    public class MeshNode
    {
        #region Public-Members

        /// <summary>
        /// Check if all remote server connections are alive.
        /// </summary>
        public bool IsHealthy
        {
            get
            {
                lock (_ClientsLock)
                {
                    foreach (MeshClient currClient in _Clients)
                    {
                        if (!currClient.Connected) return false;
                    }

                    return true;
                }
            }
        }

        /// <summary>
        /// Event to fire when a connection to the local server is successfully established.
        /// </summary>
        public event EventHandler<ServerConnectionEventArgs> PeerConnected;

        /// <summary>
        /// Event to fire when a connection to the local server is severed.
        /// </summary>
        public event EventHandler<ServerConnectionEventArgs> PeerDisconnected;
         
        /// <summary>
        /// Event to fire when a message is received from a peer.
        /// Read .ContentLength bytes from .DataStream, or, use .Data which will read the stream fully.
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Event to fire when a sync message is received from a peer and a response is expected.
        /// Read .ContentLength bytes from .DataStream, or, use .Data which will read the stream fully.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<MessageReceivedEventArgs, SyncResponse> SyncMessageReceived;

        /// <summary>
        /// Function to invoke when sending log messages.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// Serializer.
        /// </summary>
        public ISerializationHelper Serializer
        {
            get
            {
                return _Serializer;
            }
            set
            {
                if (value == null)
                {
                    _Serializer = new DefaultSerializationHelper();
                }
                else
                {
                    _Serializer = value;
                }
            }
        }

        #endregion

        #region Private-Members

        private MeshSettings _Settings = null;
        private string _Ip = null;
        private int _Port = 0;
        private string _IpPort = null;
        private bool _Ssl = false;
        private string _PfxCertificateFile = null;
        private string _PfxCertificatePass = null;
        private MeshServer _Server = null;
        private ISerializationHelper _Serializer = new DefaultSerializationHelper();

        private readonly object _PeerLock = new object();
        private List<MeshPeer> _Peers = new List<MeshPeer>();

        private readonly object _ClientsLock = new object();
        private List<MeshClient> _Clients = new List<MeshClient>();

        private ConcurrentDictionary<string, DateTime> _SyncRequests = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, PendingResponse> _PendingResponses = new ConcurrentDictionary<string, PendingResponse>();

        private Timer _Timer = new Timer();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the platform with no peers and without SSL using the default settings.
        /// </summary>
        /// <param name="ip">The IP address; either 127.0.0.1, or, an address that maps to a local network interface.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        public MeshNode(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port number must be zero or greater.");

            _Settings = new MeshSettings();
            _Ip = ip;
            _Port = port;
            _IpPort = _Ip + ":" + _Port;
            _Ssl = false;

            List<string> localIpAddresses = GetLocalIpAddresses();
            if (_Ip.Equals("127.0.0.1"))
            {
                Logger?.Invoke("[MeshNode] Loopback IP address detected; only connections from local machine will be accepted");
            }
            else
            {
                if (!localIpAddresses.Contains(_Ip))
                {
                    Logger?.Invoke("[MeshNode] Specified IP address '" + _Ip + "' not found in local IP address list:");
                    foreach (string curr in localIpAddresses) Logger?.Invoke("  " + curr);
                    throw new ArgumentException("IP address must either be 127.0.0.1 or the IP address of a local network interface.");
                }
            }

            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true;

            Logger?.Invoke("[MeshNode] Initialized MeshServer on IP:port " + _IpPort + " without SSL");
        }

        /// <summary>
        /// Instantiate the platform with no peers and without SSL.
        /// </summary>
        /// <param name="settings">Settings for the mesh network.</param> 
        /// <param name="ip">The IP address; either 127.0.0.1, or, an address that maps to a local network interface.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        public MeshNode(MeshSettings settings, string ip, int port)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port number must be zero or greater.");

            _Settings = settings;
            _Ip = ip;
            _Port = port;
            _IpPort = _Ip + ":" + _Port;
            _Ssl = false;

            List<string> localIpAddresses = GetLocalIpAddresses();
            if (_Ip.Equals("127.0.0.1"))
            {
                Logger?.Invoke("[MeshNode] Loopback IP address detected; only connections from local machine will be accepted");
            }
            else
            {
                if (!localIpAddresses.Contains(_Ip))
                {
                    Logger?.Invoke("[MeshNode] Specified IP address '" + _Ip + "' not found in local IP address list:");
                    foreach (string curr in localIpAddresses) Logger?.Invoke("  " + curr);
                    throw new ArgumentException("IP address must either be 127.0.0.1 or the IP address of a local network interface.");
                }
            }

            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true;

            Logger?.Invoke("[MeshNode] Initialized MeshServer on IP:port " + _IpPort + " without SSL");
        }

        /// <summary>
        /// Instantiate the platform with no peers with SSL.  
        /// Be sure to Start() and then Add(Peer) peers.
        /// </summary>
        /// <param name="settings">Settings for the mesh network.</param> 
        /// <param name="ip">The IP address; either 127.0.0.1, or, an address that maps to a local network interface.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="pfxCertFile">The PFX certificate file.</param>
        /// <param name="pfxCertPass">The password to the PFX certificate file.</param>
        public MeshNode(MeshSettings settings, string ip, int port, string pfxCertFile, string pfxCertPass)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (ip == null) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port number must be zero or greater.");

            _Settings = settings;
            _Ip = ip;
            _Port = port;
            _IpPort = _Ip + ":" + _Port;
            _PfxCertificateFile = pfxCertFile;
            _PfxCertificatePass = pfxCertPass;
            _Ssl = true;

            List<string> localIpAddresses = GetLocalIpAddresses();
            if (_Ip.Equals("127.0.0.1"))
            {
                Logger?.Invoke("[MeshNode] Loopback IP address detected; only connections from local machine will be accepted");
            }
            else
            {
                if (!localIpAddresses.Contains(_Ip))
                {
                    Logger?.Invoke("[MeshNode] Specified IP address '" + _Ip + "' not found in local IP address list:");
                    foreach (string curr in localIpAddresses) Logger?.Invoke("  " + curr);
                    throw new ArgumentException("IP address must either be 127.0.0.1 or the IP address of a local network interface.");
                }
            }

            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true;

            Logger?.Invoke("[MeshNode] Initialized MeshServer on IP:port " + _IpPort + " with SSL");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the mesh network server.
        /// </summary>
        public void Start()
        {
            _Server = new MeshServer(_Settings, _Ip, _Port, _Ssl, _PfxCertificateFile, _PfxCertificatePass);
            _Server.ClientConnected += MeshServerClientConnected;
            _Server.ClientDisconnected += MeshServerClientDisconnected; 
            _Server.MessageReceived += MeshServerStreamReceived;
            _Server.Logger = Logger;
            _Server.Start(); 
        }

        /// <summary>
        /// Check if a specific remote server connection is alive.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <returns>True if healthy.</returns>
        public bool IsServerConnected(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort)); 

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null) return false;
            return currClient.Connected;
        }

        /// <summary>
        /// Add a peer to the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        public void Add(MeshPeer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                bool exists = _Peers.Any(p => p.IpPort.Equals(peer.IpPort));
                if (!exists) _Peers.Add(peer);
            }

            lock (_ClientsLock)
            {
                bool exists = _Clients.Any(c => c.PeerNode.IpPort.Equals(peer.IpPort));
                if (exists) return;
                else
                {
                    MeshClient currClient = new MeshClient(_Settings, peer, _Serializer); 
                    currClient.ServerConnected += MeshClientServerConnected;
                    currClient.ServerDisconnected += MeshClientServerDisconnected;
                    // currClient.MessageReceived += MeshClientStreamReceived;
                    currClient.Logger = Logger;
                    Task.Run(() => currClient.Start());
                    _Clients.Add(currClient);
                } 
            }
        }

        /// <summary>
        /// Remove a peer from the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        public void Remove(MeshPeer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                bool exists = _Peers.Any(p => p.IpPort.Equals(peer.IpPort));
                if (exists) _Peers = _Peers.Where(p => !p.IpPort.Equals(peer.IpPort)).ToList();
            }

            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.PeerNode.IpPort.Equals(peer.IpPort)).FirstOrDefault();
                if (currClient != null && currClient != default(MeshClient))
                {
                    currClient.Dispose();
                    _Clients.Remove(currClient);
                }
            }
        }

        /// <summary>
        /// Check if a peer is part of the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <returns>True if the Peer is part of the network.</returns>
        public bool Exists(MeshPeer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                return _Peers.Any(p => p.IpPort.Equals(peer.IpPort));
            }
        }

        /// <summary>
        /// Return a list of all Peer objects.
        /// </summary>
        /// <returns>List of Peer.</returns>
        public List<MeshPeer> GetPeers()
        {
            lock (_PeerLock)
            {
                List<MeshPeer> ret = new List<MeshPeer>(_Peers);
                return ret;
            }
        }

        /// <summary>
        /// Get list of disconnected peers.
        /// </summary>
        /// <returns>List of Peer.</returns>
        public List<MeshPeer> GetDisconnectedPeers()
        {
            List<MeshPeer> ret = new List<MeshPeer>();

            lock (_ClientsLock)
            {
                foreach (MeshClient currClient in _Clients)
                {
                    if (!currClient.Connected) ret.Add(currClient.PeerNode);
                }

                return ret;
            }
        }

        /// <summary>
        /// Send string data to a peer.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(ipPort, null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send string data to a peer.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Send(ipPort, metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send byte data to a peer.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return Send(ipPort, null, data);
        }

        /// <summary>
        /// Send byte data to a peer.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, Dictionary<object, object> metadata, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            
            long contentLength = 0;
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            contentLength = data.Length;

            stream.Seek(0, SeekOrigin.Begin);
            return Send(ipPort, metadata, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer using a stream.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            return Send(ipPort, null, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer using a stream.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ipPort, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return SendInternal(currClient, MessageType.Data, metadata, contentLength, stream);
        }

        /// <summary>
        /// Send string data to a peer asynchronously.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(ipPort, null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send string data to a peer asynchronously.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAsync(ipPort, metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return await SendAsync(ipPort, null, data);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            long contentLength = 0;
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            contentLength = data.Length; 

            stream.Seek(0, SeekOrigin.Begin);
            return await SendAsync(ipPort, metadata, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return await SendInternalAsync(currClient, MessageType.Data, null, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendAsync(string ipPort, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return await SendInternalAsync(currClient, MessageType.Data, null, contentLength, stream);
        }

        /// <summary>
        /// Send string data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return SendAndWait(ipPort, timeoutMs, null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send string data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return SendAndWait(ipPort, timeoutMs, metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Send byte data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            long contentLength = 0;
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            contentLength = data.Length; 

            stream.Seek(0, SeekOrigin.Begin);
            return SendAndWait(ipPort, timeoutMs, null, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, Dictionary<object, object> metadata, byte[] data)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            long contentLength = 0;
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            contentLength = data.Length;

            stream.Seek(0, SeekOrigin.Begin);
            return SendAndWait(ipPort, timeoutMs, metadata, contentLength, stream);
        }

        /// <summary>
        /// Send stream data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, long contentLength, Stream stream)
        { 
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort)); 
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null || currClient == default(MeshClient))
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatus.PeerNotFound, 0, null);
                return failed;
            }

            return SendAndWaitInternal(currClient, MessageType.Data, timeoutMs, null, contentLength, stream);
        }

        /// <summary>
        /// Send stream data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>SyncResponse.</returns>
        public SyncResponse SendAndWait(string ipPort, int timeoutMs, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ipPort);
            if (currClient == null || currClient == default(MeshClient))
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatus.PeerNotFound, 0, null);
                return failed;
            }

            return SendAndWaitInternal(currClient, MessageType.Data, timeoutMs, metadata, contentLength, stream);
        }

        /// <summary>
        /// Broadcast string data to all nodes.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(string data)
        { 
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Broadcast(null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Broadcast string data to all nodes.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Broadcast(metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Broadcast byte data to all nodes.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return Broadcast(null, data);
        }

        /// <summary>
        /// Broadcast byte data to all nodes.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(Dictionary<object, object> metadata, byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            long contentLength = 0;
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            contentLength = data.Length; 
            stream.Seek(0, SeekOrigin.Begin);
            return Broadcast(null, contentLength, stream);
        }

        /// <summary>
        /// Broadcast stream data to all nodes.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return Broadcast(null, contentLength, stream);
        }

        /// <summary>
        /// Broadcast stream data to all nodes.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return BroadcastInternal(MessageType.Data, metadata, contentLength, stream);
        }

        /// <summary>
        /// Broadcast string data to all nodes asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await BroadcastAsync(null, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Broadcast string data to all nodes asynchronously.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(Dictionary<object, object> metadata, string data)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await BroadcastAsync(metadata, Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        /// Broadcast byte data to all nodes asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return await BroadcastAsync(null, data);
        }

        /// <summary>
        /// Broadcast byte data to all nodes asynchronously.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(Dictionary<object, object> metadata, byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data)); 
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(data);
            contentLength = data.Length; 
            stream.Seek(0, SeekOrigin.Begin);
            return await BroadcastInternalAsync(MessageType.Data, metadata, contentLength, stream);
        }

        /// <summary>
        /// Broadcast stream data to all nodes asynchronously.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
            return await BroadcastAsync(null, contentLength, stream);
        }

        /// <summary>
        /// Broadcast stream data to all nodes asynchronously.
        /// </summary>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> BroadcastAsync(Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return await BroadcastInternalAsync(MessageType.Data, metadata, contentLength, stream);
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="ipPort">Peer IP address and port, of the form IP:port.</param>
        public void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            _Server.DisconnectClient(ipPort);
        }

        #endregion

        #region Private-Methods

        private MeshPeer GetPeerByIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort)); 

            lock (_PeerLock)
            {
                MeshPeer curr = _Peers.Where(p => p.IpPort.Equals(ipPort)).FirstOrDefault();
                if (curr == null || curr == default(MeshPeer)) return null;
                return curr;
            }
        }

        private MeshClient GetMeshClientByIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort)); 

            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.PeerNode.IpPort.Equals(ipPort)).FirstOrDefault();
                if (currClient == null || currClient == default(MeshClient)) return null;
                return currClient;
            }
        }

        private MeshPeer PeerFromIpPort(string ipPort)
        {
            lock (_PeerLock)
            {
                MeshPeer curr = _Peers.Where(p => p.IpPort.Equals(ipPort)).FirstOrDefault();
                if (curr == null || curr == default(MeshPeer)) return null;
                return curr;
            }
        }
         
        #region Private-MeshClient-Callbacks

        private void MeshClientServerConnected(object sender, ServerConnectionEventArgs args)
        {
            PeerConnected?.Invoke(this, args);
        }
        
        private void MeshClientServerDisconnected(object sender, ServerConnectionEventArgs args)
        {
            PeerDisconnected?.Invoke(this, args);
        }
         
        #endregion

        #region Private-MeshServer-Callbacks
         
        private void MeshServerClientConnected(object sender, ClientConnectionEventArgs args) 
        { 
        }
          
        private void MeshServerClientDisconnected(object sender, ClientConnectionEventArgs args) 
        {  
        }
         
        private void MeshServerStreamReceived(object sender, StreamReceivedEventArgs args)
        {
            try
            {
                Message currMsg = new Message(args.DataStream, _Settings.StreamBufferSize); 

                MeshPeer currPeer = GetPeerByIpPort(currMsg.SourceIpPort);
                if (currPeer == null || currPeer == default(MeshPeer))
                {
                    Logger?.Invoke("[MeshServer] ServerStreamReceived unsolicited message from " + currMsg.SourceIpPort + ", no peer found");
                    return;
                }

                MeshClient currClient = GetMeshClientByIpPort(currPeer.IpPort);
                if (currClient == null || currClient == default(MeshClient))
                {
                    Logger?.Invoke("[MeshServer] ServerStreamReceived unable to find client for peer " + currPeer.IpPort);
                    return;
                }

                MessageReceivedEventArgs payloadArgs = new MessageReceivedEventArgs(currMsg);

                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(payloadArgs);
                        syncResponse.DataStream.Seek(0, SeekOrigin.Begin); 
                        Message responseMsg = new Message(_Serializer, _IpPort, currPeer.IpPort, currMsg.TimeoutMs, false, false, true, currMsg.Type, currMsg.Metadata, syncResponse.ContentLength, syncResponse.DataStream);
                        responseMsg.Id = currMsg.Id;  
                        SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses    
                    currMsg.DataStream.Seek(0, SeekOrigin.Begin);
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp); 
                }
                else
                {
                    MessageReceived?.Invoke(this, payloadArgs);
                }
            }
            catch (Exception e)
            {
                Logger?.Invoke("[MeshNode] StreamReceived exception: " + Environment.NewLine + _Serializer.SerializeJson(e, true));
            } 
        }

        #endregion

        #region Private-Message-Methods
         
        private bool SendInternal(MeshClient client, MessageType msgType, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            Message msg = new Message(_Serializer, _IpPort, client.PeerNode.IpPort, 0, false, false, false, msgType, metadata, contentLength, stream);
            byte[] headerBytes = msg.ToHeaderBytes();
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                        totalLen += bytesRead;
                    }
                }
            }

            ms.Seek(0, SeekOrigin.Begin);
            return client.Send(totalLen, ms);
        }

        private async Task<bool> SendInternalAsync(MeshClient client, MessageType msgType, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            Message msg = new Message(_Serializer, _IpPort, client.PeerNode.IpPort, 0, false, false, false, msgType, metadata, contentLength, stream);
            byte[] headerBytes = msg.ToHeaderBytes();  
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                        totalLen += bytesRead;
                    }
                }
            }

            ms.Seek(0, SeekOrigin.Begin); 
            return await client.SendAsync(totalLen, ms);
        }

        #endregion

        #region Private-Broadcast-Methods
         
        private bool BroadcastInternal(MessageType msgType, Dictionary<object, object> metadata, long contentLength, Stream stream)
        {
            Message msg = new Message(_Serializer, _IpPort, "0.0.0.0:0", 0, true, false, false, msgType, metadata, contentLength, stream);
            byte[] headerBytes = msg.ToHeaderBytes();
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                        totalLen += bytesRead;
                    }
                }
            }

            bool success = true;
            List<MeshClient> currClients = null;

            lock (_ClientsLock)
            {
                currClients = new List<MeshClient>(_Clients);
            }

            foreach (MeshClient currClient in _Clients)
            {
                ms.Seek(0, SeekOrigin.Begin);
                success = success && currClient.Send(totalLen, ms);
            }

            return success;
        }
         
        private async Task<bool> BroadcastInternalAsync(MessageType msgType, Dictionary<object, object> metadata, long contentLength, Stream stream)
        { 
            Message msg = new Message(_Serializer, _IpPort, "0.0.0.0:0", 0, true, false, false, msgType, metadata, contentLength, stream); 
            byte[] headerBytes = msg.ToHeaderBytes();
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                        bytesRemaining -= bytesRead;
                        totalLen += bytesRead;
                    }
                }
            }

            bool success = true;
            List<MeshClient> currClients = null;

            lock (_ClientsLock)
            {
                currClients = new List<MeshClient>(_Clients);
            }
             
            foreach (MeshClient currClient in _Clients)
            {
                ms.Seek(0, SeekOrigin.Begin);
                success = success && await currClient.SendAsync(totalLen, ms);
            } 

            return success; 
        }

        #endregion

        #region Private-Sync-Message-Methods

        private bool AddSyncRequest(string id, int timeoutMs)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (_SyncRequests.ContainsKey(id)) return false;
            return _SyncRequests.TryAdd(id, DateTime.Now.AddMilliseconds(timeoutMs));
        }
         
        private SyncResponse SendAndWaitInternal(MeshClient client, MessageType msgType, int timeoutMs, Dictionary<object, object> metadata, long contentLength, Stream stream)
        { 
            Message msg = new Message(_Serializer, _IpPort, client.PeerNode.IpPort, timeoutMs, false, true, false, msgType, metadata, contentLength, stream);
            byte[] headers = msg.ToHeaderBytes();

            try
            {
                if (!AddSyncRequest(msg.Id, timeoutMs))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatus.Failed, 0, null);
                    return failed;
                }

                MemoryStream ms = new MemoryStream();
                ms.Write(headers, 0, headers.Length);

                long totalLength = headers.Length;
                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        bytesRemaining -= bytesRead;
                        totalLength += bytesRead;
                        ms.Write(buffer, 0, bytesRead);
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);

                if (!client.Send(totalLength, ms))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatus.SendFailure, 0, null);
                    return failed;
                }

                return GetSyncResponse(msg.Id, timeoutMs);
            }
            catch (Exception e)
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatus.Failed, 0, null);
                failed.Exception = e;
                return failed;
            }
            finally
            {
                DateTime ts;
                if (_SyncRequests.ContainsKey(msg.Id)) _SyncRequests.TryRemove(msg.Id, out ts);
            }
        }

        private bool SendSyncResponseInternal(MeshClient client, Message msg)
        {
            if (msg.DataStream != null)
            {
                byte[] headers = msg.ToHeaderBytes();
                MemoryStream ms = new MemoryStream();
                ms.Write(headers, 0, headers.Length);

                long totalLength = headers.Length;
                long bytesRemaining = msg.ContentLength;
                int bytesRead = 0;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = msg.DataStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        bytesRemaining -= bytesRead;
                        totalLength += bytesRead;
                        ms.Write(buffer, 0, bytesRead);
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);
                return client.Send(totalLength, ms);
            }
            else
            {
                // nothing to send
                return false;
            }
        }
          
        private SyncResponse GetSyncResponse(string id, int timeoutMs) 
        { 
            DateTime start = DateTime.Now;

            int iterations = 0;
            while (true)
            {
                PendingResponse pendingResp = null;

                if (_PendingResponses.ContainsKey(id))
                {
                    if (!_PendingResponses.TryGetValue(id, out pendingResp))
                    {
                        SyncResponse failed = new SyncResponse(SyncResponseStatus.Failed, 0, null);
                        return failed;
                    }

                    Message respMsg = pendingResp.ResponseMessage;
                    DateTime expiration = pendingResp.Expiration;

                    if (DateTime.Now > expiration)
                    {
                        SyncResponse failed = new SyncResponse(SyncResponseStatus.Expired, 0, null);
                        return failed; 
                    }

                    SyncResponse success = new SyncResponse(SyncResponseStatus.Success, respMsg.ContentLength, respMsg.DataStream);
                    return success;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    _PendingResponses.TryRemove(id, out pendingResp);

                    SyncResponse failed = new SyncResponse(SyncResponseStatus.Expired, 0, null);
                    return failed;
                }

                iterations++;
                continue;
            }
        }

        private void CleanupThread(object source, ElapsedEventArgs args)
        {
            foreach (KeyValuePair<string, DateTime> curr in _SyncRequests)
            {
                if (curr.Value < DateTime.Now)
                {
                    DateTime ts; 
                    _SyncRequests.TryRemove(curr.Key, out ts);
                }
            }

            foreach (KeyValuePair<string, PendingResponse> curr in _PendingResponses)
            {
                if (curr.Value.Expiration < DateTime.Now)
                {
                    PendingResponse temp; 
                    _PendingResponses.TryRemove(curr.Key, out temp);
                }
            }
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

        #endregion
    }
}
