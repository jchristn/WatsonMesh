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
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// Event to fire when a sync message is received from a peer and a response is expected.
        /// Read .ContentLength bytes from .DataStream, or, use .Data which will read the stream fully.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<MessageReceivedEventArgs, Task<SyncResponse>> SyncMessageReceived;

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

        private string _Header = "[MeshNode] ";
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
            _Server.MessageReceived += MeshServerStreamReceived;
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
        public async Task<bool> Send(Guid guid, string data, Dictionary<object, object> metadata = null, CancellationToken token = default)
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
        public async Task<bool> Send(Guid guid, byte[] data, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            long contentLength = data.Length;
            stream.Seek(0, SeekOrigin.Begin);
            return await Send(guid, contentLength, stream, metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <param name="metadata">Metadata dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(Guid guid, long contentLength, Stream stream, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByGuid(guid);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return await SendInternal(currClient, MessageTypeEnum.Data, contentLength, stream, metadata, token).ConfigureAwait(false);
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
        public async Task<SyncResponse> SendAndWait(Guid guid, int timeoutMs, string data, Dictionary<object, object> metadata = null, CancellationToken token = default)
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
        public async Task<SyncResponse> SendAndWait(Guid guid, int timeoutMs, byte[] data, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MemoryStream stream = new MemoryStream();
            stream.Write(data, 0, data.Length);
            long contentLength = data.Length; 
            stream.Seek(0, SeekOrigin.Begin);
            return await SendAndWait(guid, timeoutMs, contentLength, stream, metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send stream data to a peer and wait for a response for the specified timeout duration.
        /// </summary>
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendAndWait(Guid guid, int timeoutMs, long contentLength, Stream stream, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByGuid(guid);
            if (currClient == null || currClient == default(MeshClient))
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.PeerNotFound, 0, null);
                return failed;
            }

            return await SendAndWaitInternal(currClient, MessageTypeEnum.Data, timeoutMs, contentLength, stream, metadata, token).ConfigureAwait(false);
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
        public async Task<bool> Broadcast(string data, Dictionary<object, object> metadata = null, CancellationToken token = default)
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
        public async Task<bool> Broadcast(byte[] data, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data)); 
            MemoryStream stream = new MemoryStream(data);
            long contentLength = data.Length; 
            stream.Seek(0, SeekOrigin.Begin);
            return await BroadcastInternal(MessageTypeEnum.Data, contentLength, stream, metadata, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcast stream data to all nodes asynchronously.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <param name="metadata">Metadata to include with the message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Broadcast(long contentLength, Stream stream, Dictionary<object, object> metadata = null, CancellationToken token = default)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero bytes.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return await BroadcastInternal(MessageTypeEnum.Data, contentLength, stream, metadata, token).ConfigureAwait(false);
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
         
        private async void MeshServerStreamReceived(object sender, StreamReceivedEventArgs args)
        {
            try
            {
                Message currMsg = new Message(args.DataStream, _Settings.StreamBufferSize); 

                MeshPeer currPeer = GetPeerByGuid(currMsg.SourceGuid);
                if (currPeer == null || currPeer == default(MeshPeer))
                {
                    Logger?.Invoke(_Header + "unsolicited message from " + currMsg.SourceIpPort + ", no peer found");
                    return;
                }

                MeshClient currClient = GetMeshClientByGuid(currPeer.Guid);
                if (currClient == null || currClient == default(MeshClient))
                {
                    Logger?.Invoke(_Header + "unable to find client for peer " + currPeer.IpPort);
                    return;
                }

                MessageReceivedEventArgs payloadArgs = new MessageReceivedEventArgs(currMsg);

                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = await SyncMessageReceived(payloadArgs);
                        syncResponse.DataStream.Seek(0, SeekOrigin.Begin); 
                        Message responseMsg = new Message(_Serializer, _IpPort, currPeer.IpPort, currMsg.TimeoutMs, false, false, true, currMsg.Type, currMsg.Metadata, syncResponse.ContentLength, syncResponse.DataStream);
                        responseMsg.Id = currMsg.Id;  
                        await SendSyncResponseInternal(currClient, responseMsg);
                    }
                    else
                    {
                        Logger?.Invoke(_Header + "no handler configured for sync requests, ignoring");
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
                Logger?.Invoke(_Header + "StreamReceived exception: " + Environment.NewLine + _Serializer.SerializeJson(e, true));
            } 
        }

        #endregion

        #region Private-Message-Methods
         
        private async Task<bool> SendInternal(MeshClient client, MessageTypeEnum msgType, long contentLength, Stream stream, Dictionary<object, object> metadata, CancellationToken token = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            Message msg = new Message(_Serializer, _IpPort, client.PeerNode.IpPort, 0, false, false, false, msgType, metadata, contentLength, stream);
            byte[] headerBytes = msg.ToHeaderBytes();  
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                        bytesRemaining -= bytesRead;
                        totalLen += bytesRead;
                    }
                }
            }

            ms.Seek(0, SeekOrigin.Begin); 
            return await client.SendAsync(totalLen, ms).ConfigureAwait(false);
        }

        private async Task<bool> BroadcastInternal(MessageTypeEnum msgType, long contentLength, Stream stream, Dictionary<object, object> metadata, CancellationToken token = default)
        { 
            Message msg = new Message(_Serializer, _IpPort, "0.0.0.0:0", 0, true, false, false, msgType, metadata, contentLength, stream); 
            byte[] headerBytes = msg.ToHeaderBytes();
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(headerBytes, 0, headerBytes.Length, token).ConfigureAwait(false);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        await ms.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
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
                success = success && await currClient.SendAsync(totalLen, ms, token).ConfigureAwait(false);
            } 

            return success; 
        }
         
        private bool AddSyncRequest(string id, int timeoutMs)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (_SyncRequests.ContainsKey(id)) return false;
            return _SyncRequests.TryAdd(id, DateTime.Now.AddMilliseconds(timeoutMs));
        }
         
        private async Task<SyncResponse> SendAndWaitInternal(MeshClient client, MessageTypeEnum msgType, int timeoutMs, long contentLength, Stream stream, Dictionary<object, object> metadata, CancellationToken token = default)
        { 
            Message msg = new Message(_Serializer, _IpPort, client.PeerNode.IpPort, timeoutMs, false, true, false, msgType, metadata, contentLength, stream);
            byte[] headers = msg.ToHeaderBytes();

            try
            {
                if (!AddSyncRequest(msg.Id, timeoutMs))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, 0, null);
                    return failed;
                }

                MemoryStream ms = new MemoryStream();
                await ms.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);

                long totalLength = headers.Length;
                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        bytesRemaining -= bytesRead;
                        totalLength += bytesRead;
                        await ms.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                    }
                }
                
                ms.Seek(0, SeekOrigin.Begin);

                if (!await client.SendAsync(totalLength, ms, token).ConfigureAwait(false))
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.SendFailure, 0, null);
                    return failed;
                }

                return GetSyncResponse(msg.Id, timeoutMs);
            }
            catch (Exception e)
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, 0, null);
                failed.Exception = e;
                return failed;
            }
            finally
            {
                DateTime ts;
                if (_SyncRequests.ContainsKey(msg.Id)) _SyncRequests.TryRemove(msg.Id, out ts);
            }
        }

        private async Task<bool> SendSyncResponseInternal(MeshClient client, Message msg, CancellationToken token = default)
        {
            if (msg.DataStream != null)
            {
                byte[] headers = msg.ToHeaderBytes();
                MemoryStream ms = new MemoryStream();
                await ms.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);

                long totalLength = headers.Length;
                long bytesRemaining = msg.ContentLength;
                int bytesRead = 0;
                byte[] buffer = new byte[_Settings.StreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = await msg.DataStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        bytesRemaining -= bytesRead;
                        totalLength += bytesRead;
                        await ms.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);
                return await client.SendAsync(totalLength, ms, token).ConfigureAwait(false);
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
                        SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Failed, 0, null);
                        return failed;
                    }

                    Message respMsg = pendingResp.ResponseMessage;
                    DateTime expiration = pendingResp.Expiration;

                    if (DateTime.Now > expiration)
                    {
                        SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Expired, 0, null);
                        return failed; 
                    }

                    SyncResponse success = new SyncResponse(SyncResponseStatusEnum.Success, respMsg.ContentLength, respMsg.DataStream);
                    return success;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    _PendingResponses.TryRemove(id, out pendingResp);

                    SyncResponse failed = new SyncResponse(SyncResponseStatusEnum.Expired, 0, null);
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
