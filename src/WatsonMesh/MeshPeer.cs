using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonTcp;

namespace WatsonMesh
{
    /// <summary>
    /// A peer WatsonMesh node.
    /// </summary>
    public class MeshPeer
    {
        #region Public-Members

        /// <summary>
        /// GUID.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// IP address of the peer.
        /// </summary>
        public string Ip
        {
            get
            {
                return _Ip;
            }
        }

        /// <summary>
        /// TCP port number of the peer on which a connection should be attempted.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
        }

        /// <summary>
        /// Server IP address and port of the node, of the form IP:Port.
        /// </summary>
        public string IpPort 
        { 
            get
            {
                return _IpPort;
            }
        }
 
        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; }

        /// <summary>
        /// PFX SSL certificate path and filename.
        /// </summary>
        public string PfxCertificateFile { get; }

        /// <summary>
        /// Password for PFX SSL certificate file.
        /// </summary>
        public string PfxCertificatePassword { get; }

        #endregion

        #region Private-Members

        private string _Ip = null;
        private int _Port = -1;
        private string _IpPort = null;

        #endregion

        #region Constructors-and-Factories

        internal MeshPeer()
        {

        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.  
        /// This constructor does not support SSL certificate files or passwords.
        /// </summary> 
        /// <param name="ipPort">IP address of the peer and port, of the form IP:port.  You can only use 127.0.0.1 or an IP address assigned to one of your interfaces.</param>
        public MeshPeer(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _Ip, out _Port);
            if (String.IsNullOrEmpty(_Ip)) throw new ArgumentException("Unable to extract IP address from supplied IP:port.");
            if (_Port < 1) throw new ArgumentException("Invalid port value or unable to extract port value from supplied IP:port.");

            _IpPort = ipPort;
            Ssl = false;

            PfxCertificateFile = null;
            PfxCertificatePassword = null;
        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.  This constructor supports SSL, but does not support SSL certificate files or passwords.
        /// </summary> 
        /// <param name="ipPort">IP address of the peer and port, of the form IP:port.</param>
        /// <param name="ssl">True if using SSL.</param>
        public MeshPeer(string ipPort, bool ssl)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _Ip, out _Port);
            if (String.IsNullOrEmpty(_Ip)) throw new ArgumentException("Unable to extract IP address from supplied IP:port.");
            if (_Port < 1) throw new ArgumentException("Invalid port value or unable to extract port value from supplied IP:port.");

            _IpPort = ipPort;
            Ssl = ssl;

            PfxCertificateFile = null;
            PfxCertificatePassword = null;
        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.
        /// </summary> 
        /// <param name="ipPort">IP address of the peer and port, of the form IP:port.</param>
        /// <param name="ssl">True if using SSL.</param>
        /// <param name="pfxCertFile">PFX SSL certificate path and filename.</param>
        /// <param name="pfxCertPass">Password for PFX SSL certificate file.</param>
        public MeshPeer(string ipPort, bool ssl, string pfxCertFile, string pfxCertPass)
        { 
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _Ip, out _Port);
            if (String.IsNullOrEmpty(_Ip)) throw new ArgumentException("Unable to extract IP address from supplied IP:port.");
            if (_Port < 1) throw new ArgumentException("Invalid port value or unable to extract port value from supplied IP:port.");

            _IpPort = ipPort;
            Ssl = ssl;

            PfxCertificateFile = pfxCertFile;
            PfxCertificatePassword = pfxCertPass;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human-readable String representation of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret = "[";
            ret += IpPort; 
            if (Ssl) ret += " SSL";
            else ret += " TCP";
            ret += "]";
            return ret;
        }

        #endregion

        #region Private-Methods
         
        #endregion
    }
}
