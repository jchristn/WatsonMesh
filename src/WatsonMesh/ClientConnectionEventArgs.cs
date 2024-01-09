using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonMesh
{
    /// <summary>
    /// Event arguments passed when a client connects or disconnects.
    /// </summary>
    public class ClientConnectionEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Client metadata.
        /// </summary>
        public string IpPort { get; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ipPort">IP:port.</param>
        public ClientConnectionEventArgs(string ipPort)
        {
            IpPort = ipPort;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
