using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

using WatsonTcp;

namespace Watson
{
    /// <summary>
    /// Watson mesh networking library.
    /// </summary>
    public class WatsonMesh
    {
        #region Public-Members
         
        /// <summary>
        /// Function to call when a peer connection is successfully established.
        /// </summary>
        public Func<Peer, Task> PeerConnected = null;

        /// <summary>
        /// Function to call when a peer connection is severed.
        /// </summary>
        public Func<Peer, Task> PeerDisconnected = null;
         
        /// <summary>
        /// Function to call when a message is received from a peer.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<Peer, long, Stream, Task> MessageReceived = null;

        /// <summary>
        /// Function to call when a sync message is received from a peer and a response is expected.
        /// Read the specified number of bytes from the stream.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<Peer, long, Stream, SyncResponse> SyncMessageReceived = null;
 
        #endregion

        #region Private-Members

        private MeshSettings _Settings;
        private Peer _Self;
        private MeshServer _Server;

        private readonly object _PeerLock;
        private List<Peer> _Peers;

        private readonly object _ClientsLock;
        private List<MeshClient> _Clients;

        private ConcurrentDictionary<string, DateTime> _SyncRequests;
        private ConcurrentDictionary<string, PendingResponse> _PendingResponses;

        private Timer _Timer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the platform with no peers.  Be sure to StartServer() after, and then Add(Peer) peers.
        /// </summary>
        /// <param name="settings">Settings for the mesh network.</param>
        /// <param name="self">Local server configuration.</param>
        public WatsonMesh(MeshSettings settings, Peer self)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (self == null) throw new ArgumentNullException(nameof(self));

            _Settings = settings;
            _Self = self;

            _PeerLock = new object();
            _Peers = new List<Peer>();

            _ClientsLock = new object();
            _Clients = new List<MeshClient>();

            _SyncRequests = new ConcurrentDictionary<string, DateTime>();
            _PendingResponses = new ConcurrentDictionary<string, PendingResponse>();

            _Timer = new Timer();
            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the mesh network server.
        /// </summary>
        public void Start()
        {
            _Server = new MeshServer(_Settings, _Self);
            _Server.ClientConnected = MeshServerClientConnected;
            _Server.ClientDisconnected = MeshServerClientDisconnected;
            _Server.MessageReceived = null;
            _Server.MessageReceived = MeshServerStreamReceived; 
            _Server.Start(); 
        }

        /// <summary>
        /// Check if all remote server connections are alive.
        /// </summary>
        /// <returns>True if all peers are connected.</returns>
        public bool IsHealthy()
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

        /// <summary>
        /// Check if a specific remote server connection is alive.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <returns>True if healthy.</returns>
        public bool IsHealthy(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null) return false;
            return currClient.Connected;
        }

        /// <summary>
        /// Add a peer to the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        public void Add(Peer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                bool exists = _Peers.Any(p => p.Ip.Equals(peer.Ip) && p.Port.Equals(peer.Port));
                if (exists)
                {
                    _Peers = _Peers.Where(p => !p.Ip.Equals(peer.Ip) && !p.Port.Equals(peer.Port)).ToList();
                }
                _Peers.Add(peer);
            }

            lock (_ClientsLock)
            {
                bool exists = _Clients.Any(c => c.Peer.Ip.Equals(peer.Ip) && c.Peer.Port.Equals(peer.Port));
                if (exists)
                {
                    return;
                }
                else
                {
                    MeshClient currClient = new MeshClient(_Settings, peer); 
                    currClient.ServerConnected = MeshClientServerConnected;
                    currClient.ServerDisconnected = MeshClientServerDisconnected;
                    currClient.MessageReceived = null;
                    currClient.MessageReceived = MeshClientStreamReceived; 

                    Task.Run(() => currClient.Start());

                    _Clients.Add(currClient);
                } 
            }
        }

        /// <summary>
        /// Remove a peer from the network.
        /// </summary>
        /// <param name="peer">Peer.</param>
        public void Remove(Peer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                Peer currPeer = _Peers.Where(p => p.Ip.Equals(peer.Ip) && p.Port.Equals(peer.Port)).FirstOrDefault();
                if (currPeer == null || currPeer == default(Peer)) 
                {
                }
                else
                {
                    _Peers.Remove(currPeer);
                }
            }

            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.Peer.Ip.Equals(peer.Ip) && c.Peer.Port.Equals(peer.Port)).FirstOrDefault();
                if (currClient == null || currClient == default(MeshClient))
                { 
                }
                else
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
        public bool Exists(Peer peer)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            lock (_PeerLock)
            {
                return _Peers.Any(p => p.Ip.Equals(peer.Ip) && p.Port.Equals(peer.Port));
            }
        }

        /// <summary>
        /// Return a list of all Peer objects.
        /// </summary>
        /// <returns>List of Peer.</returns>
        public List<Peer> GetPeers()
        {
            lock (_PeerLock)
            {
                List<Peer> ret = new List<Peer>(_Peers);
                return ret;
            }
        }

        /// <summary>
        /// Get list of disconnected peers.
        /// </summary>
        /// <returns>List of Peer.</returns>
        public List<Peer> GetDisconnectedPeers()
        {
            List<Peer> ret = new List<Peer>();

            lock (_ClientsLock)
            {
                foreach (MeshClient currClient in _Clients)
                {
                    if (!currClient.Connected) ret.Add(currClient.Peer);
                }

                return ret;
            }
        }
         
        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(Peer peer, byte[] data)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            long contentLength = 0;
            MemoryStream stream = null;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream();
                stream.Write(data, 0, data.Length);
                contentLength = data.Length;
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await Send(peer.Ip, peer.Port, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(Peer peer, long contentLength, Stream stream)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater bytes.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            return await Send(peer.Ip, peer.Port, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string ip, int port, byte[] data)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            long contentLength = 0;
            MemoryStream stream = null;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream();
                stream.Write(data, 0, data.Length);
                contentLength = data.Length;
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await Send(ip, port, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string ip, int port, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return await SendInternal(currClient, MessageType.Data, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer synchronously and await a response.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendSync(Peer peer, int timeoutMs, byte[] data)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");

            long contentLength = 0;
            MemoryStream stream = null;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream();
                stream.Write(data, 0, data.Length);
                contentLength = data.Length;
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await SendSync(peer.Ip, peer.Port, timeoutMs, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer synchronously and await a response.
        /// </summary>
        /// <param name="peer">Peer IP address.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="contentLength">Number of bytes to send from the stream.</param>
        /// <param name="stream">Stream containing the data to send.</param> 
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendSync(Peer peer, int timeoutMs, long contentLength, Stream stream)
        {
            if (peer == null) throw new ArgumentNullException(nameof(peer));
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");

            return await SendSync(peer.Ip, peer.Port, timeoutMs, contentLength, stream);
        }

        /// <summary>
        /// Send stream data to a peer synchronously and await a response.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="data">Data.</param>
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendSync(string ip, int port, int timeoutMs, byte[] data)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");

            long contentLength = 0;
            MemoryStream stream = null;

            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream();
                stream.Write(data, 0, data.Length);
                contentLength = data.Length; 
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await SendSync(ip, port, timeoutMs, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer synchronously and await a response.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="contentLength">Number of bytes to send from the stream.</param>
        /// <param name="stream">Stream containing the data to send.</param> 
        /// <returns>SyncResponse.</returns>
        public async Task<SyncResponse> SendSync(string ip, int port, int timeoutMs, long contentLength, Stream stream)
        { 
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be greater than zero.");
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient))
            {
                SyncResponse failed = new SyncResponse(SyncResponseStatus.PeerNotFound, 0, null);
                return failed;
            }

            return await SendSyncRequestInternal(currClient, MessageType.Data, timeoutMs, contentLength, stream);
        }

        /// <summary>
        /// Broadcast byte data to all peers.
        /// </summary>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Broadcast(byte[] data)
        {
            long contentLength = 0;
            MemoryStream stream = null;
            if (data != null && data.Length > 0)
            {
                stream = new MemoryStream(data);
                contentLength = data.Length; 
            }
            else
            {
                stream = new MemoryStream(new byte[0]);
            }

            stream.Seek(0, SeekOrigin.Begin);
            return await Broadcast(contentLength, stream);
        }

        /// <summary>
        /// Broadcast byte data to all peers.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Broadcast(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
            return await BroadcastInternal(MessageType.Data, contentLength, stream);
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="ipPort">IP address and port of the remote client, of the form IP:port.</param>
        public void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            _Server.DisconnectClient(ipPort);
        }

        #endregion

        #region Private-Methods

        private Peer GetPeerByIpPort(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            lock (_PeerLock)
            {
                Peer curr = _Peers.Where(p => p.Ip.Equals(ip) && p.Port.Equals(port)).FirstOrDefault();
                if (curr == null || curr == default(Peer)) return null;
                return curr;
            }
        }

        private MeshClient GetMeshClientByIpPort(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            lock (_ClientsLock)
            {
                MeshClient currClient = _Clients.Where(c => c.Peer.Ip.Equals(ip) && c.Peer.Port.Equals(port)).FirstOrDefault();
                if (currClient == null || currClient == default(MeshClient)) return null;
                return currClient;
            }
        }

        private Peer PeerFromIpPort(string ipPort)
        {
            lock (_PeerLock)
            {
                Peer curr = _Peers.Where(p => p.IpPort.Equals(ipPort)).FirstOrDefault();
                if (curr == null || curr == default(Peer)) return null;
                return curr;
            }
        }

        private void ParseIpPortString(string ipPort, out string ip, out int port)
        {
            int ipAddressLength = ipPort.LastIndexOf(':');
            ip = ipPort.Substring(0, ipAddressLength);
            port = Convert.ToInt32(ipPort.Substring(ipAddressLength + 1));
        }

        #region Private-MeshClient-Callbacks

        private async Task MeshClientServerConnected(Peer peer)
        { 
            if (PeerConnected != null) await PeerConnected(peer);
        }
        
        private async Task MeshClientServerDisconnected(Peer peer)
        { 
            if (PeerDisconnected != null) await PeerDisconnected(peer);
        }
         
        private async Task MeshClientStreamReceived(Peer peer, long contentLength, Stream stream)
        { 
            try
            {
                Message currMsg = new Message(stream, _Settings.ReadStreamBufferSize);

                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(peer, currMsg.ContentLength, currMsg.Data);
                        syncResponse.Data.Seek(0, SeekOrigin.Begin); 
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, peer.Ip, peer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.ContentLength, syncResponse.Data);
                        responseMsg.Id = currMsg.Id;
                        MeshClient currClient = GetMeshClientByIpPort(peer.Ip, peer.Port);
                        SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses 
                    currMsg.Data.Seek(0, SeekOrigin.Begin);
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp);
                }
                else
                {
                    if (MessageReceived != null) await MessageReceived(peer, currMsg.ContentLength, currMsg.Data);
                }
            }
            catch (Exception)
            {
            }
        }

        #endregion

        #region Private-MeshServer-Callbacks

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task MeshServerClientConnected(string ipPort)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        { 
        }
         
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task MeshServerClientDisconnected(string ipPort)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {  
        }
         
        private async Task MeshServerStreamReceived(string ipPort, long contentLength, Stream stream)
        { 
            try
            {
                Message currMsg = new Message(stream, _Settings.ReadStreamBufferSize); 

                Peer currPeer = GetPeerByIpPort(currMsg.SourceIp, currMsg.SourcePort);
                if (currPeer == null || currPeer == default(Peer)) return;
                 
                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(currPeer, currMsg.ContentLength, currMsg.Data);
                        syncResponse.Data.Seek(0, SeekOrigin.Begin); 
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, currPeer.Ip, currPeer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.ContentLength, syncResponse.Data);
                        responseMsg.Id = currMsg.Id; 
                        MeshClient currClient = GetMeshClientByIpPort(currPeer.Ip, currPeer.Port); 
                        SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses    
                    currMsg.Data.Seek(0, SeekOrigin.Begin);
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp); 
                }
                else
                {
                    if (MessageReceived != null) await MessageReceived(currPeer, currMsg.ContentLength, currMsg.Data);
                }
            }
            catch (Exception)
            {
            } 
        }

        #endregion

        #region Private-Async-Message-Methods
         
        private async Task<bool> SendInternal(MeshClient client, MessageType msgType, long contentLength, Stream stream)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            Message msg = new Message(_Self.Ip, _Self.Port, client.Peer.Ip, client.Peer.Port, 0, false, false, msgType, contentLength, stream);
            byte[] headerBytes = msg.ToHeaderBytes();  
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.ReadStreamBufferSize];

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
            return await client.Send(totalLen, ms);
        }
         
        private async Task<bool> BroadcastInternal(MessageType msgType, long contentLength, Stream stream)
        { 
            Message msg = new Message(_Self.Ip, _Self.Port, "0.0.0.0", 0, 0, false, false, msgType, contentLength, stream); 
            byte[] headerBytes = msg.ToHeaderBytes();
            long totalLen = headerBytes.Length;

            MemoryStream ms = new MemoryStream();
            ms.Write(headerBytes, 0, headerBytes.Length);

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);

                int bytesRead = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[_Settings.ReadStreamBufferSize];

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
                success = success && await currClient.Send(totalLen, ms);
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
         
        private async Task<SyncResponse> SendSyncRequestInternal(MeshClient client, MessageType msgType, int timeoutMs, long contentLength, Stream stream)
        { 
            Message msg = new Message(_Self.Ip, _Self.Port, client.Peer.Ip, client.Peer.Port, timeoutMs, true, false, msgType, contentLength, stream);
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
                byte[] buffer = new byte[_Settings.ReadStreamBufferSize];

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

                if (!client.Send(totalLength, ms).Result)
                {
                    SyncResponse failed = new SyncResponse(SyncResponseStatus.SendFailure, 0, null);
                    return failed;
                }

                return await GetSyncResponse(msg.Id, timeoutMs);
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
            if (msg.Data != null)
            {
                byte[] headers = msg.ToHeaderBytes();
                MemoryStream ms = new MemoryStream();
                ms.Write(headers, 0, headers.Length);

                long totalLength = headers.Length;
                long bytesRemaining = msg.ContentLength;
                int bytesRead = 0;
                byte[] buffer = new byte[_Settings.ReadStreamBufferSize];

                while (bytesRemaining > 0)
                {
                    bytesRead = msg.Data.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        bytesRemaining -= bytesRead;
                        totalLength += bytesRead;
                        ms.Write(buffer, 0, bytesRead);
                    }
                }

                ms.Seek(0, SeekOrigin.Begin);
                return client.Send(totalLength, ms).Result;
            }
            else
            {
                // nothing to send
                return false;
            }
        }
         
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async Task<SyncResponse> GetSyncResponse(string id, int timeoutMs)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
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

                    SyncResponse success = new SyncResponse(SyncResponseStatus.Success, respMsg.ContentLength, respMsg.Data);
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

        #endregion

        #endregion
    }
}
