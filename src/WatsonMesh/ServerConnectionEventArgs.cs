namespace WatsonMesh
{
    using System;

    /// <summary>
    /// Event arguments passed when a connection is established on the local mesh server.
    /// </summary>
    public class ServerConnectionEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// The peer object.
        /// </summary>
        public MeshPeer PeerNode { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        internal ServerConnectionEventArgs(MeshPeer peer)
        {
            PeerNode = peer;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
