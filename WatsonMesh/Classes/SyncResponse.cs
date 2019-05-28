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
        /// Response data.
        /// </summary>
        public byte[] Data
        {
            get
            {
                return _Data;
            }
            set
            {
                if (value != null)
                {
                    _Data = new byte[value.Length];
                    Buffer.BlockCopy(value, 0, _Data, 0, value.Length);
                    _ContentLength = value.Length;
                }
                else
                {
                    _Data = null;
                    _ContentLength = 0;
                }
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
                _Data = null;
            }
        }

        #endregion

        #region Private-Members

        private long _ContentLength;
        private byte[] _Data;
        private Stream _DataStream;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SyncResponse()
        {
            _ContentLength = 0;
            _Data = null;
            _DataStream = null;
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="data">Byte array containing response data.</param>
        public SyncResponse(byte[] data)
        {
            _ContentLength = 0;
            _Data = null;
            _DataStream = null;

            if (data != null && data.Length > 0)
            {
                _ContentLength = data.Length;
                _Data = new byte[data.Length];
                Buffer.BlockCopy(data, 0, _Data, 0, data.Length);
            }
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing response data.</param>
        public SyncResponse(long contentLength, Stream stream)
        {
            _ContentLength = 0;
            _Data = null;
            _DataStream = null;

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
