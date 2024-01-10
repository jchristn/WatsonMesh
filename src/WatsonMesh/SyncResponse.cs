namespace WatsonMesh
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Object encapsulating a response to a synchronous message.
    /// </summary>
    public class SyncResponse
    {
        #region Public-Members

        /// <summary>
        /// Synchronous response status.
        /// </summary>
        public SyncResponseStatusEnum Status { get; set; } = SyncResponseStatusEnum.Unknown;

        /// <summary>
        /// Data.
        /// </summary>
        public byte[] Data { get; set; } = null;

        /// <summary>
        /// Exception associated with failure, if applicable.
        /// </summary>
        public Exception Exception { get; set; } = null;

        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories
         
        private SyncResponse()
        {   
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="status">Response status.</param>
        /// <param name="data">Data.</param>
        public SyncResponse(SyncResponseStatusEnum status, string data)
        {
            Status = status;

            if (String.IsNullOrEmpty(data)) Data = Array.Empty<byte>();
            else Data = Encoding.UTF8.GetBytes(data);
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="status">Response status.</param>
        /// <param name="data">Data.</param>
        public SyncResponse(SyncResponseStatusEnum status, byte[] data)
        {
            Status = status;

            if (data == null) Data = Array.Empty<byte>();
            else Data = data;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
