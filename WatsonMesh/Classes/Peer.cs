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
    /// A peer WatsonTcp server.
    /// </summary>
    public class Peer
    {
        #region Public-Members
         
        /// <summary>
        /// Server IP address.
        /// </summary>
        public string Ip { get; set; }

        /// <summary>
        /// Server port number.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Server IP address and port, of the form IP:port, used as an identifier.
        /// </summary>
        public string IpPort { get; set; }
         
        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; set; }

        /// <summary>
        /// PFX SSL certificate path and filename.
        /// </summary>
        public string PfxCertificateFile { get; set; }

        /// <summary>
        /// Password for PFX SSL certificate file.
        /// </summary>
        public string PfxCertificatePassword { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public Peer()
        {

        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.  This constructor does not support SSL certificate files or passwords.
        /// </summary> 
        /// <param name="ip">IP address of the peer.</param>
        /// <param name="port">Port number of the peer.</param> 
        public Peer(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            Ip = ip;
            Port = port;
            IpPort = ip + ":" + port;
            Ssl = false;

            PfxCertificateFile = null;
            PfxCertificatePassword = null;
        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.  This constructor supports SSL, but does not support SSL certificate files or passwords.
        /// </summary> 
        /// <param name="ip">IP address of the peer.</param>
        /// <param name="port">Port number of the peer.</param>
        /// <param name="ssl">True if using SSL.</param>
        public Peer(string ip, int port, bool ssl)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            Ip = ip;
            Port = port;
            IpPort = ip + ":" + port;
            Ssl = ssl;

            PfxCertificateFile = null;
            PfxCertificatePassword = null;
        }

        /// <summary>
        /// Instantiate the object.  Call 'Connect()' method after instantiating and assigning values.
        /// </summary> 
        /// <param name="ip">IP address of the peer.</param>
        /// <param name="port">Port number of the peer.</param>
        /// <param name="ssl">True if using SSL.</param>
        /// <param name="pfxCertFile">PFX SSL certificate path and filename.</param>
        /// <param name="pfxCertPass">Password for PFX SSL certificate file.</param>
        public Peer(string ip, int port, bool ssl, string pfxCertFile, string pfxCertPass)
        { 
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");
             
            Ip = ip;
            Port = port;
            IpPort = ip + ":" + port;
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
