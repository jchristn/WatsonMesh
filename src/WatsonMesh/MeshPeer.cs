namespace WatsonMesh
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonTcp;

    /// <summary>
    /// A peer WatsonMesh node.
    /// </summary>
    public class MeshPeer
    {
        #region Public-Members

        /// <summary>
        /// GUID.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

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
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// PFX SSL certificate path and filename.
        /// </summary>
        public string PfxCertificateFile { get; set; } = null;

        /// <summary>
        /// Password for PFX SSL certificate file.
        /// </summary>
        public string PfxCertificatePassword { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Ip = null;
        private int _Port = 0;
        private string _IpPort = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.
        /// </summary> 
        /// <param name="guid">GUID of the peer.</param>
        /// <param name="ipPort">IP address and port of the peer server, of the form IP:port.</param>
        /// <param name="ssl">True if using SSL.</param>
        /// <param name="pfxCertFile">PFX SSL certificate path and filename.</param>
        /// <param name="pfxCertPass">Password for PFX SSL certificate file.</param>
        public MeshPeer(Guid guid,string ipPort, bool ssl = false, string pfxCertFile = null, string pfxCertPass = null)
        { 
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _Ip, out _Port);
            if (String.IsNullOrEmpty(_Ip)) throw new ArgumentException("Unable to extract IP address from supplied IP:port.");
            if (_Port < 1) throw new ArgumentException("Invalid port value or unable to extract port value from supplied IP:port.");

            _IpPort = ipPort;
            Guid = guid;
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
            string ret =
                "["
                + Guid
                + "|"
                + IpPort;
            
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
