﻿namespace WatsonMesh
{
    using System;

    internal class PendingResponse
    {
        #region Internal-Members
         
        internal DateTime Expiration;
        internal Message ResponseMessage;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        internal PendingResponse(DateTime expiration, Message msg)
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
