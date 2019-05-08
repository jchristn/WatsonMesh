using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    /// <summary>
    /// Settings for the mesh network.
    /// </summary>
    public class MeshSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Indicate whether or not to automatically reconnect when a connection is severed.
        /// </summary>
        public bool AutomaticReconnect { get; set; }

        /// <summary>
        /// Reconnect attempt interval, in milliseconds.
        /// </summary>
        public int ReconnectIntervalMs { get; set; }

        /// <summary>
        /// Shared secret password to use to mutually authenticate mesh network members.
        /// </summary>
        public string PresharedKey { get; set; }
         
        /// <summary>
        /// Enable or disable acceptance of invalid or unverifiable SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates { get; set; }

        /// <summary>
        /// Enable or disable mutual authentication when using SSL.
        /// </summary>
        public bool MutuallyAuthenticate { get; set; }
         
        /// <summary>
        /// Enable or disable reading of the data stream.
        /// When enabled, use SyncMessageReceived and AsyncMessageReceived.
        /// When disabled, use SyncStreamReceived and AsyncStreamReceived.
        /// </summary>
        public bool ReadDataStream { get; set; }

        /// <summary>
        /// Buffer size to use when reading input and output streams.  Default is 65536.
        /// </summary>
        public int ReadStreamBufferSize
        {
            get
            {
                return _ReadStreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Read stream buffer size must be greater than zero.");
                _ReadStreamBufferSize = value;
            }
        }

        #endregion

        #region Private-Members

        private int _ReadStreamBufferSize = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public MeshSettings()
        {
            Debug = false;
            AutomaticReconnect = true;
            ReconnectIntervalMs = 1000;
            PresharedKey = "default"; 
            AcceptInvalidCertificates = true;
            MutuallyAuthenticate = false; 
            ReadDataStream = true; 

            _ReadStreamBufferSize = 65536;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
