using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonMesh
{
    internal class MeshClient : IDisposable
    {
        #region Public-Members
         
        internal MeshPeer PeerNode = null;
        internal Func<string> AuthenticationRequested = null; 
        internal event EventHandler AuthenticationSucceeded; 
        internal event EventHandler AuthenticationFailed;  
        internal event EventHandler<ServerConnectionEventArgs> ServerConnected; 
        internal event EventHandler<ServerConnectionEventArgs> ServerDisconnected;
        // internal event EventHandler<MessageReceivedEventArgs> MessageReceived;
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

            Logger?.Invoke("[MeshClient] Initialized to connect to " + PeerNode.IpPort);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the client and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Logger?.Invoke("[MeshClient] Disposing");
            Dispose(true);
            GC.SuppressFinalize(this);
            Logger?.Invoke("[MeshClient] Disposed");
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

                Logger?.Invoke("[MeshClient] Starting TCP client with SSL to connect to " + ip + ":" + port);
            }
            else
            {
                _TcpClient = new WatsonTcpClient(
                    ip,
                    port);

                Logger?.Invoke("[MeshClient] Starting TCP client to connect to " + ip + ":" + port);
            }

            _TcpClient.Settings.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates; 
            _TcpClient.Settings.MutuallyAuthenticate = _Settings.MutuallyAuthenticate; 
            _TcpClient.Settings.StreamBufferSize = _Settings.StreamBufferSize; 

            //_TcpClient.Events.AuthenticationRequested = MeshClientAuthenticationRequested;
            _TcpClient.Events.AuthenticationSucceeded += MeshClientAuthenticationSucceeded;
            _TcpClient.Events.AuthenticationFailure += MeshClientAuthenticationFailure;
            _TcpClient.Events.ServerConnected += MeshClientServerConnected;
            _TcpClient.Events.ServerDisconnected += MeshClientServerDisconnected;
            _TcpClient.Events.StreamReceived += MeshClientStreamReceived;

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
                Logger?.Invoke("[MeshClient] Client exception: " + Environment.NewLine + Common.SerializeJson(e, true));
                Task unawaited = Task.Run(() => ReconnectToServer());
                ServerDisconnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
            } 

            Logger?.Invoke("[MeshClient] Client started");
        }
        
        internal bool Send(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));

            try
            {
                return _TcpClient.Send(data);
            }
            catch (Exception e)
            {
                Logger?.Invoke("[MeshClient] Send exception: " + Environment.NewLine + Common.SerializeJson(e, true));
                return false;
            }
        }

        internal bool Send(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            try
            {
                stream.Seek(0, SeekOrigin.Begin);
                return _TcpClient.Send(contentLength, stream);
            }
            catch (Exception e)
            {
                Logger?.Invoke("[MeshClient] Send exception: " + Environment.NewLine + Common.SerializeJson(e, true));
                return false;
            }
        }

        internal async Task<bool> SendAsync(byte[] data)
        {
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
             
            try
            {
                return await _TcpClient.SendAsync(data);
            }
            catch (Exception e)
            {
                Logger?.Invoke("[MeshClient] SendAsync exception: " + Environment.NewLine + Common.SerializeJson(e, true));
                return false;
            }
        }
         
        internal async Task<bool> SendAsync(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
             
            try
            { 
                stream.Seek(0, SeekOrigin.Begin);
                return await _TcpClient.SendAsync(contentLength, stream);
            }
            catch (Exception e)
            {
                Logger?.Invoke("[MeshClient] SendAsync exception: " + Environment.NewLine + Common.SerializeJson(e, true));
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
            Logger?.Invoke("[MeshClient] Server " + PeerNode.IpPort + " requests authentication");
            if (AuthenticationRequested != null) return AuthenticationRequested();
            if (!String.IsNullOrEmpty(_Settings.PresharedKey)) return _Settings.PresharedKey;
            else throw new AuthenticationException("Cannot authenticate using supplied preshared key to peer " + PeerNode.ToString());
        }

        private void MeshClientAuthenticationSucceeded(object sender, EventArgs args)
        {
            Logger?.Invoke("[MeshClient] Server " + PeerNode.IpPort + " authentication succeeded");
            AuthenticationSucceeded?.Invoke(this, EventArgs.Empty);
        }

        private void MeshClientAuthenticationFailure(object sender, EventArgs args)
        {
            Logger?.Invoke("[MeshClient] Server " + PeerNode.IpPort + " authentication failed");
            AuthenticationFailed?.Invoke(this, EventArgs.Empty);
        }

        private void MeshClientServerConnected(object sender, EventArgs args)
        {
            Logger?.Invoke("[MeshClient] Server " + PeerNode.IpPort + " connected");
            ServerConnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
        }

        private void MeshClientServerDisconnected(object sender, EventArgs args)
        {
            Logger?.Invoke("[MeshClient] Server " + PeerNode.IpPort + " disconnected");
            Task unawaited = Task.Run(() => ReconnectToServer());
            ServerDisconnected?.Invoke(this, new ServerConnectionEventArgs(PeerNode));
        }
         
        private void MeshClientStreamReceived(object sender, StreamReceivedEventArgs args)
        {
            Logger?.Invoke("[MeshClient] **UNSOLICITED** Message received from server " + PeerNode.IpPort + ": " + args.ContentLength + " bytes, ignoring");
        }

        private void ReconnectToServer()
        {
            if (!_Settings.AutomaticReconnect) return;

            while (true)
            { 
                try
                {
                    Task.Delay(_Settings.ReconnectIntervalMs).Wait();
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
