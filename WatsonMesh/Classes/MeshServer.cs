using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace Watson
{
    internal class MeshServer
    {
        #region Public-Members

        /// <summary>
        /// Function to call when a connection is established with a remote client.
        /// </summary>
        public Func<string, bool> ClientConnected = null;

        /// <summary>
        /// Function to call when a connection is severed with a remote client.
        /// </summary>
        public Func<string, bool> ClientDisconnected = null;

        /// <summary>
        /// Function to call when a message is received from a remote client.
        /// </summary>
        public Func<string, byte[], bool> ClientMessageReceived = null;

        #endregion

        #region Private-Members

        private MeshSettings _Settings;
        private Peer _Self;

        private WatsonTcpServer _TcpServer;
        private WatsonTcpSslServer _TcpSslServer;

        #endregion

        #region Constructors-and-Factories

        public MeshServer(MeshSettings settings, Peer self)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (self == null) throw new ArgumentNullException(nameof(self));

            _Settings = settings;
            _Self = self;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the Watson mesh server.
        /// </summary>
        public void StartServer()
        {
            if (_Self.Ssl)
            {
                _TcpServer = null;
                _TcpSslServer = new WatsonTcpSslServer(
                    _Self.Ip,
                    _Self.Port,
                    _Self.PfxCertificateFile,
                    _Self.PfxCertificatePassword,
                    _Settings.AcceptInvalidCertificates,
                    _Settings.SslMutualAuthentication,
                    ClientConnected,
                    ClientDisconnected,
                    ClientMessageReceived,
                    _Settings.DebugNetworking);
            }
            else
            {
                _TcpSslServer = null;
                _TcpServer = new WatsonTcpServer(
                    _Self.Ip,
                    _Self.Port,
                    ClientConnected,
                    ClientDisconnected,
                    ClientMessageReceived,
                    _Settings.DebugNetworking);
            }
        }

        /// <summary>
        /// Disconnect a remote client.
        /// </summary>
        /// <param name="ipPort">IP address and port of the remoteclient, of the form IP:port.</param>
        public void DisconnectClient(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (_Self.Ssl)
            {
                _TcpSslServer.DisconnectClient(ipPort);
            }
            else
            {
                _TcpServer.DisconnectClient(ipPort);
            }
        }

        #endregion

        #region Private-Methods
         
        #endregion
    }
}
