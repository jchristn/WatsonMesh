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
        public Func<Peer, bool> PeerConnected = null;

        /// <summary>
        /// Function to call when a peer connection is severed.
        /// </summary>
        public Func<Peer, bool> PeerDisconnected = null;

        /// <summary>
        /// Function to call when a message is received from a peer.
        /// </summary>
        public Func<Peer, byte[], bool> AsyncMessageReceived = null;

        /// <summary>
        /// Function to call when a sync message is received from a peer and a response is expected.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<Peer, byte[], SyncResponse> SyncMessageReceived = null;

        /// <summary>
        /// Function to call when a message is received from a peer.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<Peer, long, Stream, bool> AsyncStreamReceived = null;

        /// <summary>
        /// Function to call when a sync message is received from a peer and a response is expected.
        /// Read the specified number of bytes from the stream.
        /// Your function must return a SyncResponse object.
        /// </summary>
        public Func<Peer, long, Stream, SyncResponse> SyncStreamReceived = null;
 
        #endregion

        #region Private-Members

        private MeshSettings _Settings;
        private Peer _Self;
        private MeshServer _Server;

        private readonly object _PeerLock;
        private List<Peer> _Peers;

        private readonly object _ClientsLock;
        private List<MeshClient> _Clients;

        public ConcurrentDictionary<string, DateTime> _SyncRequests;
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
            _Server.MessageReceived = MeshServerMessageReceived;
            _Server.StreamReceived = MeshServerStreamReceived;
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
                    currClient.MessageReceived = MeshClientMessageReceived;
                    currClient.StreamReceived = MeshClientStreamReceived;
                     
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
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool SendAsync(Peer peer, byte[] data)
        {
            return SendAsync(peer.Ip, peer.Port, data);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool SendAsync(Peer peer, long contentLength, Stream stream)
        {
            return SendAsync(peer.Ip, peer.Port, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool SendAsync(string ip, int port, byte[] data)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return SendAsyncInternal(currClient, MessageType.Data, data);
        }

        /// <summary>
        /// Send byte data to a peer asynchronously using a stream.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool SendAsync(string ip, int port, long contentLength, Stream stream)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return SendAsyncInternal(currClient, MessageType.Data, contentLength, stream);
        }

        /// <summary>
        /// Send byte data to a peer and await a response.
        /// </summary>
        /// <param name="peer">Peer IP address.</param>
        /// <param name="timeoutMs">Number of milliseconds to wait before considering the request expired.</param>
        /// <param name="data">Peer port number.</param>
        /// <param name="response">Byte data returned by the peer.</param>
        /// <returns>True if successful.</returns>
        public bool SendSync(Peer peer, int timeoutMs, byte[] data, out byte[] response)
        {
            return SendSync(peer.Ip, peer.Port, timeoutMs, data, out response);
        }
         
        /// <summary>
        /// Send byte data to a peer and await a response.
        /// </summary>
        /// <param name="peer">Peer IP address.</param>
        /// <param name="data">Peer port number.</param>
        /// <param name="response">Byte data returned by the peer.</param>
        /// <returns>True if successful.</returns>
        public bool SendSync(string ip, int port, int timeoutMs, byte[] data, out byte[] response)
        {
            response = null;
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (timeoutMs < 1) throw new ArgumentException("Timeout must be zero or greater.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient)) return false;
            return SendSyncRequestInternal(currClient, MessageType.Data, timeoutMs, data, out response);
        }
         
        /// <summary>
        /// Broadcast byte data to all peers.
        /// </summary>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return BroadcastAsyncInternal(MessageType.Data, data);
        }

        /// <summary>
        /// Broadcast byte data to all peers.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be at least one byte.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
            return BroadcastAsyncInternal(MessageType.Data, contentLength, stream);
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

        #region Private-MeshClient-Callbacks
         
        private bool MeshClientServerConnected(Peer peer)
        { 
            if (PeerConnected != null) return PeerConnected(peer);
            return true;
        }
        
        private bool MeshClientServerDisconnected(Peer peer)
        { 
            if (PeerDisconnected != null) return PeerDisconnected(peer);
            return true;
        }

        private bool MeshClientMessageReceived(Peer peer, byte[] data)
        {
            try
            {
                Message currMsg = new Message(data, true);

                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(peer, currMsg.Data);
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, peer.Ip, peer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.Data);
                        responseMsg.Id = currMsg.Id;
                        MeshClient currClient = GetMeshClientByIpPort(peer.Ip, peer.Port);
                        return SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp);
                }
                else
                {
                    if (AsyncMessageReceived != null) return AsyncMessageReceived(peer, currMsg.Data);
                }

                return true;
            }
            catch (Exception)
            { 
                return false;
            }
        }

        private bool MeshClientStreamReceived(Peer peer, long contentLength, Stream stream)
        {
            try
            {
                Message currMsg = new Message(stream, _Settings.ReadStreamBufferSize);

                if (currMsg.SyncRequest)
                {
                    if (SyncStreamReceived != null)
                    {
                        SyncResponse syncResponse = SyncStreamReceived(peer, currMsg.ContentLength, currMsg.DataStream); 
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, peer.Ip, peer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.Data); 
                        responseMsg.Id = currMsg.Id;
                        MeshClient currClient = GetMeshClientByIpPort(peer.Ip, peer.Port);
                        return SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp);
                }
                else
                {
                    if (AsyncStreamReceived != null)
                    {
                        return AsyncStreamReceived(peer, currMsg.ContentLength, currMsg.DataStream);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

        #region Private-MeshServer-Callbacks

        private bool MeshServerClientConnected(string ipPort)
        { 
            return true;
        }
         
        private bool MeshServerClientDisconnected(string ipPort)
        { 
            return true;
        }

        private bool MeshServerMessageReceived(string ipPort, byte[] data)
        { 
            try
            {
                Message currMsg = new Message(data, true); 

                Peer currPeer = GetPeerByIpPort(currMsg.SourceIp, currMsg.SourcePort);
                if (currPeer == null || currPeer == default(Peer))
                { 
                    return false;
                }

                if (currMsg.SyncRequest)
                {
                    if (SyncMessageReceived != null)
                    {
                        SyncResponse syncResponse = SyncMessageReceived(currPeer, currMsg.Data);
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, currPeer.Ip, currPeer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.Data);
                        responseMsg.Id = currMsg.Id;
                        MeshClient currClient = GetMeshClientByIpPort(currPeer.Ip, currPeer.Port);
                        return SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses 
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp);
                    return true;
                }
                else
                {
                    if (AsyncMessageReceived != null)
                    { 
                        return AsyncMessageReceived(currPeer, currMsg.Data);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool MeshServerStreamReceived(string ipPort, long contentLength, Stream stream)
        { 
            try
            {
                Message currMsg = new Message(stream, _Settings.ReadStreamBufferSize); 

                Peer currPeer = GetPeerByIpPort(currMsg.SourceIp, currMsg.SourcePort);
                if (currPeer == null || currPeer == default(Peer))
                { 
                    return false;
                }
                 
                if (currMsg.SyncRequest)
                {
                    if (SyncStreamReceived != null)
                    {
                        SyncResponse syncResponse = SyncStreamReceived(currPeer, currMsg.ContentLength, currMsg.DataStream); 
                        Message responseMsg = new Message(_Self.Ip, _Self.Port, currPeer.Ip, currPeer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, syncResponse.Data); 
                        responseMsg.Id = currMsg.Id;
                        MeshClient currClient = GetMeshClientByIpPort(currPeer.Ip, currPeer.Port);
                        return SendSyncResponseInternal(currClient, responseMsg);
                    }
                }
                else if (currMsg.SyncResponse)
                {
                    // add to sync responses   
                    PendingResponse pendingResp = new PendingResponse(DateTime.Now.AddMilliseconds(currMsg.TimeoutMs), currMsg);
                    _PendingResponses.TryAdd(currMsg.Id, pendingResp);
                    return true;
                }
                else
                {
                    if (AsyncStreamReceived != null)
                    {
                        return AsyncStreamReceived(currPeer, currMsg.ContentLength, currMsg.DataStream);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            } 
        }

        #endregion

        #region Private-Utility-Methods

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

        private bool SendAsyncInternal(MeshClient client, MessageType msgType, byte[] data)
        {
            Message msg = new Message(_Self.Ip, _Self.Port, client.Peer.Ip, client.Peer.Port, 0, false, false, msgType, data);
            byte[] msgData = msg.ToBytes();
            return client.Send(msgData).Result;
        }

        private bool SendAsyncInternal(MeshClient client, MessageType msgType, long contentLength, Stream stream)
        {
            Message msg = new Message(_Self.Ip, _Self.Port, client.Peer.Ip, client.Peer.Port, 0, false, false, msgType, contentLength, stream);
            byte[] msgData = msg.ToBytes();
            return client.Send(msgData).Result;
        }

        private bool BroadcastAsyncInternal(MessageType msgType, byte[] data)
        { 
            Message msg = new Message(_Self.Ip, _Self.Port, "0.0.0.0", 0, 0, false, false, msgType, data);
            byte[] msgData = msg.ToBytes();

            bool success = true;

            lock (_ClientsLock)
            {
                foreach (MeshClient currClient in _Clients)
                {
                    success = success && currClient.Send(msgData).Result;
                }
            }

            return success; 
        }

        private bool BroadcastAsyncInternal(MessageType msgType, long contentLength, Stream stream)
        { 
            Message msg = new Message(_Self.Ip, _Self.Port, "0.0.0.0", 0, 0, false, false, msgType, contentLength, stream);
            byte[] msgData = msg.ToBytes();

            bool success = true;

            lock (_ClientsLock)
            {
                foreach (MeshClient currClient in _Clients)
                {
                    success = success && currClient.Send(msgData).Result;
                }
            }

            return success; 
        }

        private bool SendSyncRequestInternal(MeshClient client, MessageType msgType, int timeoutMs, byte[] data, out byte[] response)
        {
            response = null;
            Message msg = new Message(_Self.Ip, _Self.Port, client.Peer.Ip, client.Peer.Port, timeoutMs, true, false, msgType, data);
            byte[] msgData = msg.ToBytes();

            try
            {
                if (!AddSyncRequest(msg.Id, timeoutMs)) return false;
                if (!client.Send(msgData).Result) return false;
                return GetSyncResponse(msg.Id, timeoutMs, out response);
            }
            catch (Exception)
            { 
                return false;
            }
            finally
            {
                DateTime ts;
                if (_SyncRequests.ContainsKey(msg.Id)) _SyncRequests.TryRemove(msg.Id, out ts);
            }
        }
         
        private bool SendSyncResponseInternal(MeshClient client, Message msg )
        { 
            byte[] msgData = msg.ToBytes();
            return client.Send(msgData).Result; 
        }

        #endregion

        #region Private-Sync-Message-Methods

        private bool AddSyncRequest(string id, int timeoutMs)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (_SyncRequests.ContainsKey(id)) return false;
            return _SyncRequests.TryAdd(id, DateTime.Now.AddMilliseconds(timeoutMs));
        }

        private bool GetSyncResponse(string id, int timeoutMs, out byte[] response)
        {
            response = null;
            DateTime start = DateTime.Now;

            int iterations = 0;
            while (true)
            {
                PendingResponse pendingResp = null;

                if (_PendingResponses.ContainsKey(id))
                {
                    if (!_PendingResponses.TryGetValue(id, out pendingResp)) return false;

                    Message respMsg = pendingResp.ResponseMessage;
                    DateTime expiration = pendingResp.Expiration;

                    if (DateTime.Now > expiration) return false;

                    int dataLen = 0;
                    if (respMsg.Data != null) dataLen = respMsg.Data.Length;
                    response = new byte[dataLen];
                    if (dataLen > 0)
                    {
                        Buffer.BlockCopy(respMsg.Data, 0, response, 0, dataLen);
                    }

                    _PendingResponses.TryRemove(id, out pendingResp);
                    return true;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    response = null;
                    _PendingResponses.TryRemove(id, out pendingResp);
                    return false;
                }

                iterations++;
                continue;
            }
        }

        private bool GetSyncResponse(string id, int timeoutMs, out long contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = null;
            DateTime start = DateTime.Now;

            int iterations = 0;
            while (true)
            {
                PendingResponse pendingResp = null;

                if (_PendingResponses.ContainsKey(id))
                {
                    if (!_PendingResponses.TryGetValue(id, out pendingResp)) return false;

                    Message respMsg = pendingResp.ResponseMessage;
                    DateTime expiration = pendingResp.Expiration;

                    if (DateTime.Now > expiration) return false;

                    contentLength = respMsg.ContentLength;
                    stream = respMsg.DataStream;

                    _PendingResponses.TryRemove(id, out pendingResp);
                    return true;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    contentLength = 0;
                    stream = null;

                    _PendingResponses.TryRemove(id, out pendingResp);
                    return false;
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
