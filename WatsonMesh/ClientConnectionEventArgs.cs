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
        internal ClientConnectionEventArgs(string ipPort)
        {
            IpPort = ipPort;
        }

        /// <summary>
        /// The IP:port of the client.
        /// </summary>
        public string IpPort { get; } 
    }
}
