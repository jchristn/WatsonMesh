using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonMesh
{
    /// <summary>
    /// Event arguments passed when a connection is established on the local mesh server.
    /// </summary>
    public class ServerConnectionEventArgs : EventArgs
    {
        internal ServerConnectionEventArgs(MeshPeer peer)
        {
            PeerNode = peer;
        }

        /// <summary>
        /// The peer object.
        /// </summary>
        public MeshPeer PeerNode { get; } 
    }
}
