namespace WatsonMesh
{
    using System;

    /// <summary>
    /// Settings for the mesh network.
    /// </summary>
    public class MeshSettings
    {
        #region Public-Members

        /// <summary>
        /// Indicate whether or not to automatically reconnect when a connection is severed.
        /// </summary>
        public bool AutomaticReconnect = true;

        /// <summary>
        /// Reconnect attempt interval, in milliseconds.
        /// </summary>
        public int ReconnectIntervalMs
        {
            get
            {
                return _ReconnectInternalMs;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Reconnect interval must be greater than zero.");
                _ReconnectInternalMs = value;
            }
        }

        /// <summary>
        /// Shared secret password to use to mutually authenticate mesh network members.
        /// </summary>
        public string PresharedKey = null;

        /// <summary>
        /// Enable or disable acceptance of invalid or unverifiable SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates = true;

        /// <summary>
        /// Enable or disable mutual authentication when using SSL.
        /// </summary>
        public bool MutuallyAuthenticate = false;
          
        /// <summary>
        /// Buffer size to use when reading input and output streams.  Default is 65536.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Stream buffer size must be greater than zero.");
                _StreamBufferSize = value;
            }
        }

        /// <summary>
        /// GUID for the mesh node.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        #endregion

        #region Private-Members

        private int _ReconnectInternalMs = 1000;
        private int _StreamBufferSize = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public MeshSettings()
        {  
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
