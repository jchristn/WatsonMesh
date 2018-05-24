using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Watson
{
    internal class MeshClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// The peer object.
        /// </summary>
        public Peer Peer { get; private set; }

        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<Peer, bool> ServerConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<Peer, bool> ServerDisconnected = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// </summary>
        public Func<Peer, byte[], bool> ServerMessageReceived = null;

        #endregion

        #region Private-Members

        private bool _Disposed = false;

        private MeshSettings _Settings;

        private WatsonTcpClient _TcpClient;
        private WatsonTcpSslClient _TcpSslClient;

        #endregion

        #region Constructors-and-Factories
        
        public MeshClient(MeshSettings settings, Peer peer)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            _Settings = settings;
            Peer = peer;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Establish TCP (with or without SSL) connection to the peer server.
        /// </summary>
        public void Connect()
        {
            try
            {
                if (Peer.Ssl)
                {
                    _TcpClient = null;
                    _TcpSslClient = new WatsonTcpSslClient(
                        Peer.Ip,
                        Peer.Port,
                        Peer.PfxCertificateFile,
                        Peer.PfxCertificatePassword,
                        _Settings.AcceptInvalidCertificates,
                        _Settings.SslMutualAuthentication,
                        _ServerConnected,
                        _ServerDisconnected,
                        _ServerMessageReceived,
                        _Settings.DebugNetworking);
                }
                else
                {
                    _TcpSslClient = null;
                    _TcpClient = new WatsonTcpClient(
                        Peer.Ip,
                        Peer.Port,
                        _ServerConnected,
                        _ServerDisconnected,
                        _ServerMessageReceived,
                        _Settings.DebugNetworking);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unable to connect to peer " + Peer.ToString() + " due to exception: " + e.ToString());

                Task.Run(() => ReconnectToServer());
            }
        }

        /// <summary>
        /// Check if the local client is connected to the remote server.
        /// </summary>
        /// <returns>True if connected.</returns>
        public bool IsConnected()
        {
            if (Peer.Ssl)
            {
                if (_TcpSslClient == null) return false;
                return _TcpSslClient.IsConnected();
            }
            else
            {
                if (_TcpClient == null) return false;
                return _TcpClient.IsConnected();
            }
        }

        /// <summary>
        /// Send data to the remote server.
        /// </summary>
        /// <param name="data">Byte data to send.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            if (_TcpClient != null) return await _TcpClient.SendAsync(data);
            else if (_TcpClient != null) return await _TcpSslClient.SendAsync(data);

            Debug.WriteLine("No connection to peer: " + Peer.ToString());
            return false;
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
                if (_TcpClient != null) _TcpClient.Dispose();
                if (_TcpSslClient != null) _TcpSslClient.Dispose();
            }

            _Disposed = true;
        }

        private bool _ServerConnected()
        {
            Debug.WriteLine("Peer server connected: " + Peer.ToString());
            if (ServerConnected != null) return ServerConnected(Peer);
            else return true;
        }

        private bool _ServerDisconnected()
        {
            Debug.WriteLine("Peer server disconnected: " + Peer.ToString()); 

            Task.Run(() => ReconnectToServer());

            if (ServerDisconnected != null) return ServerDisconnected(Peer);
            else return true;
        }

        private bool _ServerMessageReceived(byte[] data)
        { 
            if (ServerMessageReceived != null) return ServerMessageReceived(Peer, data);
            else return true;
        }

        private void ReconnectToServer()
        {
            if (!_Settings.AutomaticReconnect)
            {
                Debug.WriteLine("Reconnect to " + Peer.ToString() + " disabled by settings");
                return;
            }

            while (true)
            {
                try
                {
                    Debug.WriteLine("Reconnect to " + Peer.ToString() + " pending in " + _Settings.ReconnectIntervalMs + "ms");
                    Task.Delay(_Settings.ReconnectIntervalMs).Wait();
                    Connect();
                    Debug.WriteLine("Reconnect to " + Peer.ToString() + " successful");
                    break;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Reconnect to " + Peer.ToString() + " failed, reattempting (" + e.Message + ")");
                }
            }
        }

        #endregion
    }
}
