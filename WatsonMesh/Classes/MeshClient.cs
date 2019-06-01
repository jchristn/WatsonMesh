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
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug = false;

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
        public Func<bool> AuthenticationSucceeded = null;

        /// <summary>
        /// Function to call when authentication failed.
        /// </summary>
        public Func<bool> AuthenticationFailure = null;

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
        public Func<Peer, byte[], bool> MessageReceived = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// Read the specified number of bytes from the stream.
        /// </summary>
        public Func<Peer, long, Stream, bool> StreamReceived = null;
          
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
            _TcpClient.ReadDataStream = _Settings.ReadDataStream;
            _TcpClient.ReadStreamBufferSize = _Settings.ReadStreamBufferSize;

            _TcpClient.AuthenticationRequested = MeshClientAuthenticationRequested;
            _TcpClient.AuthenticationSucceeded = MeshClientAuthenticationSucceeded;
            _TcpClient.AuthenticationFailure = MeshClientAuthenticationFailure;
            _TcpClient.ServerConnected = MeshClientServerConnected;
            _TcpClient.ServerDisconnected = MeshClientServerDisconnected;
            _TcpClient.StreamReceived = MeshClientStreamReceived;
            _TcpClient.MessageReceived = MeshClientMessageReceived;

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

        private bool MeshClientAuthenticationSucceeded()
        {
            if (AuthenticationSucceeded != null) return AuthenticationSucceeded();
            return true;
        }

        private bool MeshClientAuthenticationFailure()
        {
            if (AuthenticationFailure != null) return AuthenticationFailure();
            return true;
        }

        private bool MeshClientServerConnected()
        {
            if (ServerConnected != null)
            {
                return ServerConnected(Peer);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientServerDisconnected()
        { 
            Task.Run(() => ReconnectToServer());
            if (ServerDisconnected != null)
            {
                return ServerDisconnected(Peer);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientMessageReceived(byte[] data)
        {
            if (MessageReceived != null)
            {
                return MessageReceived(Peer, data);
            }
            else
            {
                return true;
            }
        }

        private bool MeshClientStreamReceived(long contentLength, Stream stream)
        {
            if (StreamReceived != null)
            {
                return StreamReceived(Peer, contentLength, stream);
            }
            else
            {
                return true;
            }
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
