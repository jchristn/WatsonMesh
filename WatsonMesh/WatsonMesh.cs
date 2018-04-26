using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public Func<Peer, byte[], bool> MessageReceived = null;
         
        #endregion

        #region Private-Members

        private MeshSettings _Settings;
        private Peer _Self;
        private MeshServer _Server;

        private readonly object _PeerLock;
        private List<Peer> _Peers;

        private readonly object _ClientsLock;
        private List<MeshClient> _Clients;
         
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
        /// Send byte data to a peer.
        /// </summary>
        /// <param name="peer">Peer.</param>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool Send(Peer peer, byte[] data)
        {
            return Send(peer.Ip, peer.Port, data);
        }

        /// <summary>
        /// Send byte data to a peer.
        /// </summary>
        /// <param name="ip">Peer IP address.</param>
        /// <param name="port">Peer port number.</param>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool Send(string ip, int port, byte[] data)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            MeshClient currClient = GetMeshClientByIpPort(ip, port);
            if (currClient == null || currClient == default(MeshClient))
            {
                Debug.WriteLine("Unable to find peer: " + ip + ":" + port);
                return false;
            }

            return SendInternal(currClient, MessageType.Data, data);
        }

        /// <summary>
        /// Broadcast byte data to all peers.
        /// </summary>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public bool Broadcast(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            return BroadcastInternal(MessageType.Data, data);
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
                    return null;
                }

                return currClient;
            }
        }

        #region MeshClient-Callbacks
        
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
            // already deserialized in client
            if (MessageReceived != null) return MessageReceived(peer, data);
            return true;
        }

        #endregion

        #region MeshServer-Callbacks
         
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

            string ip;
            int port;
            ParseIpPortString(currMsg.IpPort, out ip, out port);

            Peer currPeer = GetPeerByIpPort(ip, port);
            if (currPeer == null || currPeer == default(Peer))
            {
                Debug.WriteLine("Unsolicted client message received from: " + currMsg.IpPort);
                return false;
            }

            Debug.WriteLine("Message from " + currPeer.ToString() + ": " + currMsg.Data.Length + " bytes");
            return MessageReceived(currPeer, currMsg.Data);
        }

        #endregion

        #region Utility-Methods

        private Peer PeerFromIpPort(string ipPort)
        {
            lock (_PeerLock)
            {
                Peer curr = _Peers.Where(p => p.IpPort.Equals(ipPort)).FirstOrDefault();
                if (curr == null || curr == default(Peer))
                {
                    Debug.WriteLine("PeerFromIpPort could not find peer " + ipPort);
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

        private bool SendInternal(MeshClient client, MessageType msgType, byte[] data)
        {
            Message msg = new Message(_Self.IpPort, msgType, data);
            byte[] msgData = Encoding.UTF8.GetBytes(Common.SerializeJson(msg, false));
            return client.Send(msgData).Result;
        }

        private bool BroadcastInternal(MessageType msgType, byte[] data)
        {
            Message msg = new Message(_Self.IpPort, msgType, data);
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

        #endregion

        #endregion
    }
}
