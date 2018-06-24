using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// </summary>
        public Func<Peer, byte[], byte[]> SyncMessageReceived = null;

        /// <summary>
        /// Sync requests that are pending responses from a peer.
        /// </summary>
        public ConcurrentDictionary<string, DateTime> SyncRequests { get; private set; }

        /// <summary>
        /// Sync responses from peers that are awaiting processing.
        /// </summary>
        public ConcurrentDictionary<string, Tuple<Message, DateTime>> SyncResponses { get; private set; }

        /// <summary>
        /// Function to call when an issue is encountered internally and a warning message needs to be consumed by the calling application.
        /// </summary>
        public Func<string, bool> WarningMessage { get; set; }

        #endregion

        #region Private-Members

        private MeshSettings _Settings;
        private Peer _Self;
        private MeshServer _Server;

        private readonly object _PeerLock;
        private List<Peer> _Peers;

        private readonly object _ClientsLock;
        private List<MeshClient> _Clients;

        private Timer _Timer;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the platform with no peers.  Be sure to StartServer() after.
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

            SyncRequests = new ConcurrentDictionary<string, DateTime>();
            SyncResponses = new ConcurrentDictionary<string, Tuple<Message, DateTime>>();

            _Timer = new Timer();
            _Timer.Elapsed += new ElapsedEventHandler(CleanupThread);
            _Timer.Interval = 5000;
            _Timer.Enabled = true;

            WarningMessage = null;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the mesh network server.
        /// </summary>
        public void StartServer()
        {
            _Server = new MeshServer(_Settings, _Self);
            _Server.ClientConnected = ClientConnected;
            _Server.ClientDisconnected = ClientDisconnected;
            _Server.ClientMessageReceived = ClientMessageReceived;
            _Server.StartServer();

            WarningMessage?.Invoke("[WatsonMesh] Starting server");
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
                    if (!currClient.IsConnected()) return false;
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
            if (currClient == null)
            {
                WarningMessage?.Invoke("[WatsonMesh] IsHealthy unknown client " + ip + ":" + port);
                return false;
            }
            return currClient.IsConnected();
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
                    MeshClient currClient = new MeshClient(_Settings, peer, WarningMessage);
                    currClient.ServerConnected = ServerConnected;
                    currClient.ServerDisconnected = ServerDisconnected;
                    currClient.ServerMessageReceived = ServerMessageReceived;
                    Task.Run(() => currClient.Connect());
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
                    if (!currClient.IsConnected()) ret.Add(currClient.Peer);
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
            if (currClient == null || currClient == default(MeshClient))
            {
                Debug.WriteLine("Unable to find peer: " + ip + ":" + port);
                WarningMessage?.Invoke("[WatsonMesh] SendAsync unable to find peer " + ip + ":" + port);
                return false;
            }

            return SendAsyncInternal(currClient, MessageType.Data, data);
        }

        /// <summary>
        /// Send byte data to a peer and await a response.
        /// </summary>
        /// <param name="peer">Peer IP address.</param>
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
            if (currClient == null || currClient == default(MeshClient))
            {
                Debug.WriteLine("Unable to find peer: " + ip + ":" + port);
                WarningMessage?.Invoke("[WatsonMesh] SendSync unable to find peer " + ip + ":" + port);
                return false;
            }

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
                if (curr == null || curr == default(Peer))
                {
                    Debug.WriteLine("Unable to find peer: " + ip + ":" + port);
                    WarningMessage?.Invoke("[WatsonMesh] GetPeerByIpPort unable to find peer " + ip + ":" + port);
                    return null;
                }

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
                if (currClient == null || currClient == default(MeshClient))
                {
                    Debug.WriteLine("Unable to find peer: " + ip + ":" + port);
                    WarningMessage?.Invoke("[WatsonMesh] GetMeshClientByIpPort unable to find peer " + ip + ":" + port);
                    return null;
                }

                return currClient;
            }
        }

        #region Private-MeshClient-Callbacks
        
        private bool ServerConnected(Peer peer)
        {
            if (PeerConnected != null) return PeerConnected(peer);
            return true;
        }
        
        private bool ServerDisconnected(Peer peer)
        {
            if (PeerDisconnected != null) return PeerDisconnected(peer);
            return true;
        }

        private bool ServerMessageReceived(Peer peer, byte[] data)
        { 
            Message currMsg = Common.DeserializeJson<Message>(data);
            
            if (currMsg.SyncRequest)
            {
                if (SyncMessageReceived != null)
                {
                    byte[] responseData = SyncMessageReceived(peer, currMsg.Data);
                    Message responseMsg = new Message(_Self.Ip, _Self.Port, peer.Ip, peer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, responseData);
                    responseMsg.Id = currMsg.Id;
                    MeshClient currClient = GetMeshClientByIpPort(peer.Ip, peer.Port);
                    return SendSyncResponseInternal(currClient, responseMsg);
                }
            }
            else if (currMsg.SyncResponse)
            {
                // add to sync responses
                Tuple<Message, DateTime> tuple = new Tuple<Message, DateTime>(currMsg, DateTime.Now.AddMilliseconds(currMsg.TimeoutMs));
                SyncResponses.TryAdd(currMsg.Id, tuple);
            }
            else
            {
                if (AsyncMessageReceived != null) return AsyncMessageReceived(peer, currMsg.Data);
            }

            return true;
        }

        #endregion

        #region Private-MeshServer-Callbacks

        private bool ClientConnected(string ipPort)
        {
            // do nothing, we do not know the server IP:port and thus cannot determine who the peer is
            Debug.WriteLine("Client connection received from: " + ipPort);
            return true;
        }
         
        private bool ClientDisconnected(string ipPort)
        {
            // do nothing, we do not know the server IP:port and thus cannot determine who the peer is
            Debug.WriteLine("Client connection terminated from: " + ipPort);
            return true;
        }

        private bool ClientMessageReceived(string ipPort, byte[] data)
        {
            Message currMsg = Common.DeserializeJson<Message>(data);  
             
            Peer currPeer = GetPeerByIpPort(currMsg.SourceIp, currMsg.SourcePort);
            if (currPeer == null || currPeer == default(Peer))
            {
                Debug.WriteLine("Unsolicted client message received from: " + currMsg.SourceIp + ":" + currMsg.SourcePort);
                WarningMessage?.Invoke("[WatsonMesh] ClientMessageReceived discarding unsolicted message from " + currMsg.SourceIp + ":" + currMsg.SourcePort);
                return false;
            }
             
            if (currMsg.SyncRequest)
            { 
                if (SyncMessageReceived != null)
                {
                    byte[] responseData = SyncMessageReceived(currPeer, currMsg.Data);
                    Message responseMsg = new Message(_Self.Ip, _Self.Port, currPeer.Ip, currPeer.Port, currMsg.TimeoutMs, false, true, currMsg.Type, responseData);
                    responseMsg.Id = currMsg.Id;
                    MeshClient currClient = GetMeshClientByIpPort(currPeer.Ip, currPeer.Port);
                    return SendSyncResponseInternal(currClient, responseMsg);
                }
            }
            else if (currMsg.SyncResponse)
            {
                // add to sync responses 
                Tuple<Message, DateTime> tuple = new Tuple<Message, DateTime>(currMsg, DateTime.Now.AddMilliseconds(currMsg.TimeoutMs));
                SyncResponses.TryAdd(currMsg.Id, tuple);
            }
            else
            { 
                if (AsyncMessageReceived != null) AsyncMessageReceived(currPeer, currMsg.Data);
            }

            return true;
        }

        #endregion

        #region Private-Utility-Methods

        private Peer PeerFromIpPort(string ipPort)
        {
            lock (_PeerLock)
            {
                Peer curr = _Peers.Where(p => p.IpPort.Equals(ipPort)).FirstOrDefault();
                if (curr == null || curr == default(Peer))
                {
                    Debug.WriteLine("PeerFromIpPort could not find peer " + ipPort);
                    WarningMessage?.Invoke("[WatsonMesh] PeerFromIpPort unable to find peer " + ipPort);
                    return null;
                }
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
            byte[] msgData = Encoding.UTF8.GetBytes(Common.SerializeJson(msg, false));
            return client.Send(msgData).Result;
        }

        private bool BroadcastAsyncInternal(MessageType msgType, byte[] data)
        {
            Message msg = new Message(_Self.Ip, _Self.Port, "0.0.0.0", 0, 0, false, false, msgType, data);
            byte[] msgData = Encoding.UTF8.GetBytes(Common.SerializeJson(msg, false));

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
            byte[] msgData = Encoding.UTF8.GetBytes(Common.SerializeJson(msg, false));

            try
            { 
                if (!AddSyncRequest(msg.Id, timeoutMs))
                {
                    Debug.WriteLine("Unable to add sync request");
                    WarningMessage?.Invoke("[WatsonMesh] SendSyncRequestInternal unable to add sync request for message to " + msg.DestinationIp + ":" + msg.DestinationPort);
                    return false;
                }

                if (!client.Send(msgData).Result)
                {
                    Debug.WriteLine("Unable to send to peer");
                    WarningMessage?.Invoke("[WatsonMesh] SendSyncRequestInternal unable to send message to " + msg.DestinationIp + ":" + msg.DestinationPort);
                    return false;
                }

                bool success = GetSyncResponse(msg.Id, timeoutMs, out response);  
                if (success)
                {
                    Debug.WriteLine("Retrieved sync response for msg ID " + msg.Id);
                }
                else
                {
                    WarningMessage?.Invoke("[WatsonMesh] SendSyncRequestInternal unable to retrieve response from " + msg.DestinationIp + ":" + msg.DestinationPort);
                    Debug.WriteLine("Unable to retrieve sync response for msg ID " + msg.Id);
                }

                return success;
            }
            finally
            {
                DateTime ts;
                if (SyncRequests.ContainsKey(msg.Id)) SyncRequests.TryRemove(msg.Id, out ts); 
            }
        }

        private bool SendSyncResponseInternal(MeshClient client, Message message)
        {  
            byte[] msgData = Encoding.UTF8.GetBytes(Common.SerializeJson(message, false));
            return client.Send(msgData).Result; 
        }

        #endregion

        #region Private-Sync-Message-Methods

        private bool AddSyncRequest(string id, int timeoutMs)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            if (SyncRequests.ContainsKey(id)) return false;
            return SyncRequests.TryAdd(id, DateTime.Now.AddMilliseconds(timeoutMs));
        }

        private bool GetSyncResponse(string id, int timeoutMs, out byte[] response)
        { 
            response = null;
            DateTime start = DateTime.Now;

            int iterations = 0;
            while (true)
            {
                Tuple<Message, DateTime> respTuple = null;

                if (SyncResponses.ContainsKey(id))
                {
                    if (!SyncResponses.TryGetValue(id, out respTuple))
                    {
                        SyncResponses.TryRemove(id, out respTuple);
                        WarningMessage?.Invoke("[WatsonMesh] GetSyncResponse unable to get data for ID " + id);
                        return false;
                    }

                    Message respMsg = respTuple.Item1;
                    DateTime expiration = respTuple.Item2;

                    if (DateTime.Now > expiration)
                    {
                        SyncResponses.TryRemove(id, out respTuple);
                        Debug.WriteLine("Response expired");
                        WarningMessage?.Invoke("[WatsonMesh] GetSyncResponse response expired for ID " + id);
                        return false;
                    }

                    int dataLen = 0;
                    if (respMsg.Data != null) dataLen = respMsg.Data.Length;
                    response = new byte[dataLen];
                    if (dataLen > 0)
                    {
                        Buffer.BlockCopy(respMsg.Data, 0, response, 0, dataLen);
                    }

                    SyncResponses.TryRemove(id, out respTuple);
                    return true;
                }

                // Check if timeout exceeded 
                TimeSpan ts = DateTime.Now - start;
                if (ts.TotalMilliseconds > timeoutMs)
                {
                    response = null;
                    SyncResponses.TryRemove(id, out respTuple);
                    Debug.WriteLine("Timeout exceeded");
                    WarningMessage?.Invoke("[WatsonMesh] GetSyncResponse timeout exceeded for ID " + id);
                    return false;
                }

                iterations++;
                continue;
            }
        }

        private void CleanupThread(object source, ElapsedEventArgs args)
        {
            foreach (KeyValuePair<string, DateTime> curr in SyncRequests)
            {
                if (curr.Value < DateTime.Now)
                {
                    DateTime ts;
                    Debug.WriteLine("Cleanup removing expired request ID " + curr.Key);
                    WarningMessage?.Invoke("[WatsonMesh] CleanupThread removing expired request ID " + curr.Key);
                    SyncRequests.TryRemove(curr.Key, out ts);
                }
            }

            foreach (KeyValuePair<string, Tuple<Message, DateTime>> curr in SyncResponses)
            {
                if (curr.Value.Item2 < DateTime.Now)
                {
                    Tuple<Message, DateTime> tuple;
                    Debug.WriteLine("Cleanup removing expired response ID " + curr.Key);
                    WarningMessage?.Invoke("[WatsonMesh] CleanupThread removing expired response ID " + curr.Key);
                    SyncResponses.TryRemove(curr.Key, out tuple);
                }
            }
        }

        #endregion

        #endregion
    }
}
