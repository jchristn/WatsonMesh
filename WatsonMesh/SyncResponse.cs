using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WatsonMesh
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
        public Stream DataStream
        {
            get
            {
                return _DataStream;
            }
            set
            {
                if (_ContentLength <= 0) throw new ArgumentException("Set ContentLength before setting DataStream.");
                _DataStream = value;
            }
        }

        /// <summary>
        /// The data from DataStream.
        /// Using Data will fully read the contents of DataStream.
        /// </summary>
        public byte[] Data
        {
            get
            {
                if (_Data != null) return _Data;
                if (ContentLength <= 0) return null;
                _Data = Common.StreamToBytes(DataStream);
                return _Data;
            }
        }

        /// <summary>
        /// Exception associated with failure, if applicable.
        /// </summary>
        public Exception Exception = null;

        #endregion

        #region Private-Members

        private long _ContentLength = 0;
        private Stream _DataStream = null;
        private byte[] _Data = null;

        #endregion

        #region Constructors-and-Factories
         
        private SyncResponse()
        {   
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="status">Response status.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing response data.  Will only be attached if contentLength is greater than zero.</param>
        public SyncResponse(SyncResponseStatus status, long contentLength, Stream stream)
        {
            Status = status; 
            _DataStream = stream;

            if (contentLength > 0)
            {
                _ContentLength = contentLength;
                _DataStream = stream;
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
