using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Watson
{
    /// <summary>
    /// Object encapsulating a response to a synchronous message.
    /// </summary>
    public class SyncResponse
    {
        #region Public-Members

        /// <summary>
        /// Synchronous response status.
        /// </summary>
        public SyncResponseStatus Status = SyncResponseStatus.Unknown;

        /// <summary>
        /// Response data length.
        /// </summary>
        public long ContentLength
        {
            get
            {
                return _ContentLength;
            }
            set
            {
                if (value < 0) throw new ArgumentException("Content length must be zero or greater.");
                _ContentLength = value;
            }
        }

        /// <summary>
        /// Stream containing response data.  Set ContentLength first.
        /// </summary>
        public Stream Data
        {
            get
            {
                return _Data;
            }
            set
            {
                if (_ContentLength <= 0) throw new ArgumentException("Set ContentLength before setting DataStream.");
                _Data = value;
            }
        }

        /// <summary>
        /// Exception associated with failure, if applicable.
        /// </summary>
        public Exception Exception = null;

        #endregion

        #region Private-Members

        private long _ContentLength = 0;
        private Stream _Data = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SyncResponse()
        {
            Status = SyncResponseStatus.Unknown;
            _ContentLength = 0;
            _Data = null;
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing response data.</param>
        public SyncResponse(SyncResponseStatus status, long contentLength, Stream stream)
        {
            Status = status;
            _ContentLength = 0;
            _Data = null;

            if (contentLength > 0)
            {
                _ContentLength = contentLength;
                _Data = stream;
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
