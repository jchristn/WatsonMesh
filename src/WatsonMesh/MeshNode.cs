namespace WatsonMesh
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using WatsonTcp;

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
        public event EventHandler<MeshMessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Event to fire when a sync message is received from a peer and a response is expected.
        /// Read .ContentLength bytes from .DataStream, or, use .Data which will read the stream fully.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<MeshMessageReceivedEventArgs, Task<SyncResponse>> SyncMessageReceived;

        /// <summary>
        /// Function to invoke when sending log messages.
        /// </summary>
        public Action<string> Logger = null;

        #endregion

        #region Private-Members

        private string _Header = "[MeshNode] ";
        private MeshSettings _Settings = null;
        private string _Ip = null;
        private int _Port = 0;
        private string _IpPort = null;
        private bool _Ssl = false;
        private string _PfxCertificateFile = null;
        private string _PfxCertificatePass = null;
        private MeshServer _Server = null;

        private readonly object _PeerLock = new object();
        private List<MeshPeer> _Peers = new List<MeshPeer>();

        private readonly object _ClientsLock = new object();
        private List<MeshClient> _Clients = new List<MeshClient>();

        private ConcurrentDictionary<string, DateTime> _SyncRequests = new ConcurrentDictionary<string, DateTime>();
        private ConcurrentDictionary<string, PendingResponse> _PendingResponses = new ConcurrentDictionary<string, PendingResponse>();

        private System.Timers.Timer _Timer = new System.Timers.Timer();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the platform with no peers with SSL.  
        /// Be sure to Start() and then Add(Peer) peers.
        /// </summary>
        /// <param name="settings">Settings for the mesh network.</param> 
        /// <param name="ip">The IP address; either 127.0.0.1, or, an address that maps to a local network interface.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFile">The PFX certificate file.</param>
        /// <param name="pfxCertPass">The password to the PFX certificate file.</param>
        public MeshNode(
            MeshSettings settings, 
            string ip = "127.0.0.1", 
            int port = 8000, 
            bool ssl = false,
            string pfxCertFile = null, 
            string pfxCertPass = null)
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
            _Ssl = ssl;

            List<string> localIpAddresses = GetLocalIpAddresses();
            if (_Ip.Equals("127.0.0.1"))
            {
                Logger?.Invoke(_Header + "loopback IP address detected; only connections from local machine will be accepted");
            }
            else
            {
                if (!localIpAddresses.Contains(_Ip))
                {
                    Logger?.Invoke(_Header + "specified IP address '" + _Ip + "' not found in local IP address list:");
                    foreach (string curr in localIpAddresses) Logger?.Invoke("  " + curr);
                    throw new ArgumentException("IP address must either be 127.0.0.1 or the IP address of a local network interface.");
                }
            }

            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true;

            Logger?.Invoke(_Header + "initialized MeshServer on IP:port " + _IpPort);
        }

        #endregion

        #region Public-Methods

        #region Connection

        /// <summary>
        /// Start the mesh network server.
        /// </summary>
        public void Start()
        {
            _Server = new MeshServer(_Settings, _Ip, _Port, _Ssl, _PfxCertificateFile, _PfxCertificatePass);
            _Server.ClientConnected += MeshServerClientConnected;
            _Server.ClientDisconnected += MeshServerClientDisconnected; 
            _Server.MessageReceived += MeshServerMessageReceived;
            _Server.Logger = Logger;
            _Server.Start(); 
        }

        /// <summary>
        /// Check if a specific remote server connection is alive.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <returns>True if connected.</returns>
        public bool IsPeerConnected(Guid guid)
        {
            MeshClient currClient = GetMeshClientByGuid(guid);
            if (currClient == null) return false;
            return currClient.Connected;
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task DisconnectClient(Guid guid, CancellationToken token = default)
        {
            await _Server.DisconnectClient(guid, token).ConfigureAwait(false);
        }

        #endregion

        #region Peers

        /// <summary>
        /// Add a peer to the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        public void Add(MeshPeer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                bool exists = _Peers.Any(p => p.Guid.Equals(peer.Guid));
                if (!exists) _Peers.Add(peer);
            }

            lock (_ClientsLock)
            {
                bool exists = _Clients.Any(c => c.PeerNode.Guid.Equals(peer.Guid));
                if (exists) return;
                else
                {
                    MeshClient currClient = new MeshClient(_Settings, peer); 
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
        /// <param name="guid">Guid.</param>
        public void Remove(Guid guid)
        {
            lock (_PeerLock)
            {
                bool exists = _Peers.Any(p => p.Guid.Equals(guid));
                if (exists) _Peers = _Peers.Where(p => !p.Guid.Equals(guid)).ToList();
            }

            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.PeerNode.Guid.Equals(guid)).FirstOrDefault();
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
        public IEnumerable<MeshPeer> GetPeers()
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

        #endregion

        #region Send

        /// <summary>
        /// Send string data to a peer asynchronously.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(Guid guid, string data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await Send(guid, Encoding.UTF8.GetBytes(data), metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(Guid guid, byte[] data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MeshClient currClient = GetMeshClientByGuid(guid);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return await SendInternal(currClient, MessageTypeEnum.Data, data, metadata, token).ConfigureAwait(false);
        }

        #endregion

        #region SendAndWait

        /// <summary>
        /// Send string data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendAndWait(Guid guid, int timeoutMs, string data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await SendAndWait(guid, timeoutMs, Encoding.UTF8.GetBytes(data), metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send byte data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendAndWait(Guid guid, int timeoutMs, byte[] data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MeshClient currClient = GetMeshClientByGuid(guid);
            if (currClient == null || currClient == default(MeshClient))
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.PeerNotFound, Array.Empty<byte>());
                return failed;
            }
            return await SendAndWaitInternal(currClient, MessageTypeEnum.Data, timeoutMs, data, metadata, token).ConfigureAwait(false);
        }

        #endregion

        #region Broadcast

        /// <summary>
        /// Broadcast string data to all nodes asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Broadcast(string data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return await Broadcast(Encoding.UTF8.GetBytes(data), metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcast byte data to all nodes asynchronously.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Broadcast(byte[] data, Dictionary<string, object> metadata = null, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data)); 
            return await BroadcastInternal(MessageTypeEnum.Data, data, metadata, token).ConfigureAwait(false);
        }

        #endregion

        #endregion

        #region Private-Methods

        private MeshPeer GetPeerByGuid(Guid guid)
        {
            lock (_PeerLock)
            {
                MeshPeer curr = _Peers.Where(p => p.Guid.Equals(guid)).FirstOrDefault();
                if (curr == null || curr == default(MeshPeer)) return null;
                return curr;
            }
        }

        private MeshClient GetMeshClientByGuid(Guid guid)
        {
            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.PeerNode.Guid.Equals(guid)).FirstOrDefault();
                if (currClient == null || currClient == default(MeshClient)) return null;
                return currClient;
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
         
        private void MeshServerMessageReceived(object sender, MeshMessageReceivedEventArgs args)
        {
            try
            {
                MeshPeer currPeer = GetPeerByGuid(args.SourceGuid);
                if (currPeer == null || currPeer == default(MeshPeer))
                {
                    Logger?.Invoke(
                        _Header + "unsolicited message from " + args.SourceGuid + " " + args.SourceIpPort + ", no peer found" 
                        + SerializationHelper.SerializeJson(args, true));
                    return;
                }

                MeshClient currClient = GetMeshClientByGuid(currPeer.Guid);
                if (currClient == null || currClient == default(MeshClient))
                {
                    Logger?.Invoke(
                        _Header + "unable to find client for peer " + currPeer.IpPort
                        + SerializationHelper.SerializeJson(args, true));
                    return;
                }

                if (args.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(args).Result;
                        MeshMessage responseMsg = new MeshMessage(
                            _Settings.Guid,
                            currPeer.Guid,
                            _IpPort, 
                            currPeer.IpPort, 
                            args.TimeoutMs, 
                            false, false, true, 
                            args.Type, 
                            args.Metadata, 
                            syncResponse.Data);
                        responseMsg.Id = args.Id;  
                        SendSyncResponseInternal(currClient, responseMsg).Wait();
                    }
                    else
                    {
                        Logger?.Invoke(_Header + "no handler configured for sync requests, ignoring");
                    }
                }
                else if (args.SyncResponse)
                {
                    // add to sync responses
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(args.TimeoutMs), new MeshMessage(args));
                    _PendingResponses.TryAdd(args.Id, pendingResp); 
                }
                else
                {
                    MessageReceived?.Invoke(this, args);
                }
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "StreamReceived exception: " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
            } 
        }

        #endregion

        #region Private-Message-Methods
         
        private async Task<bool> SendInternal(MeshClient client, MessageTypeEnum msgType, byte[] data, Dictionary<string, object> metadata, CancellationToken token = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (data == null) data = Array.Empty<byte>();

            MeshMessage msg = new MeshMessage(
                _Settings.Guid,
                client.PeerNode.Guid,
                _IpPort, 
                client.PeerNode.IpPort, 
                0, 
                false, false, false, 
                msgType, 
                metadata, 
                data);
            metadata = AppendHeaders(metadata, msg.Headers);
            return await client.Send(data, metadata, token).ConfigureAwait(false);
        }

        private async Task<bool> BroadcastInternal(MessageTypeEnum msgType, byte[] data, Dictionary<string, object> metadata, CancellationToken token = default)
        {
            if (data == null) data = Array.Empty<byte>();
            MeshMessage msg = new MeshMessage(
                _Settings.Guid,
                default(Guid),
                _IpPort, 
                "0.0.0.0:0", 
                0, 
                true, false, false, 
                msgType, 
                metadata, 
                data);
            metadata = AppendHeaders(metadata, msg.Headers);

            bool success = true;
            List<MeshClient> currClients = null;

            lock (_ClientsLock)
            {
                currClients = new List<MeshClient>(_Clients);
            }
             
            foreach (MeshClient currClient in _Clients)
            {
                msg.DestinationGuid = currClient.PeerNode.Guid;
                success = success && await currClient.Send(data, metadata, token).ConfigureAwait(false);
            } 

            return success; 
        }
         
        private bool AddSyncRequest(string id, int timeoutMs)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (_SyncRequests.ContainsKey(id)) return false;
            return _SyncRequests.TryAdd(id, DateTime.Now.AddMilliseconds(timeoutMs));
        }
         
        private async Task<SyncResponse> SendAndWaitInternal(MeshClient client, MessageTypeEnum msgType, int timeoutMs, byte[] data, Dictionary<string, object> metadata, CancellationToken token = default)
        {
            if (data == null) data = Array.Empty<byte>();
            MeshMessage msg = new MeshMessage(
                _Settings.Guid,
                client.PeerNode.Guid,
                _IpPort, 
                client.PeerNode.IpPort, 
                timeoutMs, 
                false, true, false, 
                msgType, 
                metadata, 
                data);
            metadata = AppendHeaders(metadata, msg.Headers);

            try
            {
                if (!AddSyncRequest(msg.Id, timeoutMs))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, Array.Empty<byte>());
                    return failed;
                }

                if (!await client.Send(data, metadata, token).ConfigureAwait(false))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.SendFailure, Array.Empty<byte>());
                    return failed;
                }

                return GetSyncResponse(msg.Id, timeoutMs);
            }
            catch (Exception e)
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, Array.Empty<byte>());
                failed.Exception = e;
                return failed;
            }
            finally
            {
                DateTime ts;
                if (_SyncRequests.ContainsKey(msg.Id)) _SyncRequests.TryRemove(msg.Id, out ts);
            }
        }

        private async Task<bool> SendSyncResponseInternal(MeshClient client, MeshMessage msg, CancellationToken token = default)
        {
            if (msg.Data != null)
            {
                Dictionary<string, object> metadata = AppendHeaders(new Dictionary<string, object>(), msg.Headers);
                return await client.Send(msg.Data, metadata, token).ConfigureAwait(false);
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
                        SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, Array.Empty<byte>());
                        return failed;
                    }

                    MeshMessage respMsg = pendingResp.ResponseMessage;
                    DateTime expiration = pendingResp.Expiration;

                    if (DateTime.Now > expiration)
                    {
                        SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Expired, Array.Empty<byte>());
                        return failed; 
                    }

                    SyncResponse success = new SyncResponse(SyncResponseStatusEnum.Success, respMsg.Data);
                    return success;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    _PendingResponses.TryRemove(id, out pendingResp);

                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Expired, Array.Empty<byte>());
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

        private Dictionary<string, object> AppendHeaders(Dictionary<string, object> original, Dictionary<string, object> append)
        {
            if (original == null) original = new Dictionary<string, object>();
            if (append == null) return original;

            foreach (KeyValuePair<string, object> kvp in append)
            {
                original.Add(kvp.Key, kvp.Value);
            }

            return original;
        }

        #endregion

        #endregion
    }
}
