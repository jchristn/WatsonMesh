namespace WatsonMesh
{
    using System;

    internal class PendingResponse
    {
        #region Internal-Members
         
        internal DateTime Expiration;
        internal MeshMessage ResponseMessage;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        internal PendingResponse(DateTime expiration, MeshMessage msg)
        {
            Expiration = expiration;
            ResponseMessage = msg; 
        }

        #endregion

        #region Internal-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
