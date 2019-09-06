using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Watson
{
    /// <summary>
    /// Watson mesh networking client.
    /// </summary>
    internal class MeshClient : IDisposable
    {
        #region Public-Members
         
        /// <summary>
        /// The peer object.
        /// </summary>
        public Peer Peer { get; private set; }

        /// <summary>
        /// Function to call when authentication is requested.
        /// </summary>
        public Func<string> AuthenticationRequested = null;

        /// <summary>
        /// Function to call when authentication succeeded.
        /// </summary>
        public Func<Task> AuthenticationSucceeded = null;

        /// <summary>
        /// Function to call when authentication failed.
        /// </summary>
        public Func<Task> AuthenticationFailure = null;

        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<Peer, Task> ServerConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<Peer, Task> ServerDisconnected = null;
         
        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<Peer, long, Stream, Task> MessageReceived = null;
          
        /// <summary>
        /// Check if the local client is connected to the remote server.
        /// </summary>
        /// <returns>True if connected.</returns>
        public bool Connected
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
        
        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="peer">Peer.</param>
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
        public void Start()
        {
            if (Peer.Ssl)
            {
                _TcpClient = new WatsonTcpClient(
                    Peer.Ip,
                    Peer.Port,
                    Peer.PfxCertificateFile,
                    Peer.PfxCertificatePassword);
            }
            else
            {
                _TcpClient = new WatsonTcpClient(
                    Peer.Ip,
                    Peer.Port);
            }

            _TcpClient.AcceptInvalidCertificates = _Settings.AcceptInvalidCertificates; 
            _TcpClient.MutuallyAuthenticate = _Settings.MutuallyAuthenticate; 
            _TcpClient.ReadStreamBufferSize = _Settings.ReadStreamBufferSize;
            _TcpClient.ReadDataStream = false;
            _TcpClient.Debug = _Settings.Debug;

            _TcpClient.AuthenticationRequested = MeshClientAuthenticationRequested;
            _TcpClient.AuthenticationSucceeded = MeshClientAuthenticationSucceeded;
            _TcpClient.AuthenticationFailure = MeshClientAuthenticationFailure;
            _TcpClient.ServerConnected = MeshClientServerConnected;
            _TcpClient.ServerDisconnected = MeshClientServerDisconnected;
            _TcpClient.MessageReceived = null;
            _TcpClient.StreamReceived = MeshClientStreamReceived;

            try
            {
                _TcpClient.Start();
            }
            catch (Exception)
            {
                Task.Run(() => MeshClientServerDisconnected());
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
             
            try
            {
                return await _TcpClient.SendAsync(data);
            }
            catch (Exception)
            {  
                return false;
            }
        }

        /// <summary>
        /// Send data to the remote server.
        /// </summary>
        /// <param name="contentLength">The number of bytes to read from the stream.</param>
        /// <param name="stream">The stream containing the data to send.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, Stream stream)
        {
            if (contentLength < 1) throw new ArgumentException("Content length must be greater than zero.");
            if (stream == null || !stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");
             
            try
            { 
                stream.Seek(0, SeekOrigin.Begin);
                return await _TcpClient.SendAsync(contentLength, stream);
            }
            catch (Exception)
            { 
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
            if (AuthenticationRequested != null) return AuthenticationRequested();
            if (!String.IsNullOrEmpty(_Settings.PresharedKey)) return _Settings.PresharedKey;
            else throw new AuthenticationException("Cannot authenticate using supplied preshared key to peer " + Peer.ToString());
        }

        private async Task MeshClientAuthenticationSucceeded()
        {
            if (AuthenticationSucceeded != null) await AuthenticationSucceeded();
        }

        private async Task MeshClientAuthenticationFailure()
        {
            if (AuthenticationFailure != null) await AuthenticationFailure();
        }

        private async Task MeshClientServerConnected()
        {
            if (ServerConnected != null) await ServerConnected(Peer);
        }

        private async Task MeshClientServerDisconnected()
        { 
            Task unawaited = Task.Run(() => ReconnectToServer());
            if (ServerDisconnected != null) await ServerDisconnected(Peer);
        }
         
        private async Task MeshClientStreamReceived(long contentLength, Stream stream)
        {
            if (MessageReceived != null) await MessageReceived(Peer, contentLength, stream);
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
