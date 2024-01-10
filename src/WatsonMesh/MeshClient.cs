namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonTcp;

    internal class MeshClient : IDisposable
    {
        #region Internal-Members

        internal MeshPeer PeerNode = null;
        internal Func<string> AuthenticationRequested = null; 
        internal event EventHandler AuthenticationSucceeded; 
        internal event EventHandler AuthenticationFailed;  
        internal event EventHandler<ServerConnectionEventArgs> ServerConnected; 
        internal event EventHandler<ServerConnectionEventArgs> ServerDisconnected;
        internal Action<string> Logger = null;

        internal bool Connected
        {
            get
            {
                if (_TcpClient == null) return false;
                return _TcpClient.Connected;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[MeshClient] ";
        private bool _Disposed = false; 
        private MeshSettings _Settings; 
        private WatsonTcpClient _TcpClient;
        
        #endregion

        #region Constructors-and-Factories
         
        internal MeshClient(MeshSettings settings, MeshPeer peer)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings)); 
            if (peer == null) throw new ArgumentNullException(nameof(peer));

            _Settings = settings;

            PeerNode = peer;
            
            Logger?.Invoke(_Header + "initialized to connect to " + PeerNode.IpPort);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Logger?.Invoke(_Header + "disposing");
            Dispose(true);
            GC.SuppressFinalize(this);
            Logger?.Invoke(_Header + "disposed");
        }

        internal void Start()
        {
            string ip = null;
            int port = -1;
            Common.ParseIpPort(PeerNode.IpPort, out ip, out port);

            if (PeerNode.Ssl)
            {
                _TcpClient = new WatsonTcpClient(
                    ip,
                    port,
                    PeerNode.PfxCertificateFile,
                    PeerNode.PfxCertificatePassword);

                Logger?.Invoke(_Header + "starting ssl://" + ip + ":" + port);
            }
            else
            {
                _TcpClient = new WatsonTcpClient(
                    ip,
                    port);

                Logger?.Invoke(_Header + "starting tcp://" + ip + ":" + port);
            }

            _TcpClient.Settings.Guid = _Settings.Guid;
            _TcpClient.Settings.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates; 
            _TcpClient.Settings.MutuallyAuthenticate = _Settings.MutuallyAuthenticate; 
            _TcpClient.Settings.StreamBufferSize = _Settings.StreamBufferSize; 

            _TcpClient.Events.AuthenticationSucceeded += MeshClientAuthenticationSucceeded;
            _TcpClient.Events.AuthenticationFailure += MeshClientAuthenticationFailure;
            _TcpClient.Events.ServerConnected += MeshClientServerConnected;
            _TcpClient.Events.ServerDisconnected += MeshClientServerDisconnected;
            _TcpClient.Events.MessageReceived += MeshClientMessageReceived;

            try
            {
                _TcpClient.Connect();
            }
            catch (SocketException)
            {
                Task unawaited = Task.Run(() => ReconnectToServer());
                ServerDisconnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "client exception: " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
                Task unawaited = Task.Run(() => ReconnectToServer());
                ServerDisconnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
            } 

            Logger?.Invoke(_Header + "client started");
        }
        
        internal async Task<bool> Send(byte[] data, Dictionary<string, object> headers, CancellationToken token = default)
        {
            if (data == null) data = Array.Empty<byte>();
             
            try
            {
                return await _TcpClient.SendAsync(data, headers, 0, token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "SendAsync exception: " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
                return false;
            }
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
            }

            _Disposed = true;
        }
         
        private string MeshClientAuthenticationRequested()
        {
            Logger?.Invoke(_Header + "server " + PeerNode.IpPort + " requests authentication");
            if (AuthenticationRequested != null) return AuthenticationRequested();
            if (!String.IsNullOrEmpty(_Settings.PresharedKey)) return _Settings.PresharedKey;
            else throw new AuthenticationException("Cannot authenticate using supplied preshared key to peer " + PeerNode.ToString());
        }

        private void MeshClientAuthenticationSucceeded(object sender, EventArgs args)
        {
            Logger?.Invoke(_Header + "server " + PeerNode.IpPort + " authentication succeeded");
            AuthenticationSucceeded?.Invoke(this, EventArgs.Empty);
        }

        private void MeshClientAuthenticationFailure(object sender, EventArgs args)
        {
            Logger?.Invoke(_Header + "server " + PeerNode.IpPort + " authentication failed");
            AuthenticationFailed?.Invoke(this, EventArgs.Empty);
        }

        private void MeshClientServerConnected(object sender, EventArgs args)
        {
            Logger?.Invoke(_Header + "server " + PeerNode.IpPort + " connected");
            ServerConnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
        }

        private void MeshClientServerDisconnected(object sender, EventArgs args)
        {
            Logger?.Invoke(_Header + "server " + PeerNode.IpPort + " disconnected");
            Task unawaited = Task.Run(() => ReconnectToServer());
            ServerDisconnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
        }
         
        private void MeshClientMessageReceived(object sender, MessageReceivedEventArgs args)
        {
            Logger?.Invoke(_Header + "unsolicited message from server " + PeerNode.IpPort + ": " + args.Data.Length + " bytes, ignoring");
        }

        private async Task ReconnectToServer()
        {
            if (!_Settings.AutomaticReconnect) return;

            while (true)
            { 
                try
                {
                    await Task.Delay(_Settings.ReconnectIntervalMs);
                    Start(); 
                    break;
                }
                catch (Exception)
                { 
                }
            }
        }

        #endregion
    }
}
