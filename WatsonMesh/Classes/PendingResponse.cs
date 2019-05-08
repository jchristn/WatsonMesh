using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Watson
{
    internal class PendingResponse
    {
        #region Public-Members
         
        /// <summary>
        /// The time at which the response expires.
        /// </summary>
        public DateTime Expiration;

        /// <summary>
        /// The response message.
        /// </summary>
        public Message ResponseMessage;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary> 
        /// <param name="expiration">The time at which the response expires.</param>
        /// <param name="msg">The response message.</param>
        public PendingResponse(DateTime expiration, Message msg)
        {
            Expiration = expiration;
            ResponseMessage = msg;

            if (ResponseMessage.DataStream != null)
            {
                ResponseMessage.Data = Common.ReadStream(ResponseMessage.ContentLength, ResponseMessage.DataStream);
                ResponseMessage.DataStream = null;
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion

    }
}
