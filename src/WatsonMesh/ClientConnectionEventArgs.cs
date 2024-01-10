namespace WatsonMesh
{
    using System;

    /// <summary>
    /// Event arguments passed when a client connects or disconnects.
    /// </summary>
    public class ClientConnectionEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Client GUID.
        /// </summary>
        public Guid Guid { get; set; } = default(Guid);

        /// <summary>
        /// Client metadata.
        /// </summary>
        public string IpPort { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="guid">Guid.</param>
        /// <param name="ipPort">IP:port.</param>
        public ClientConnectionEventArgs(Guid guid, string ipPort)
        {
            Guid = guid;
            IpPort = ipPort;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
