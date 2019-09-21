using System;
using System.Collections.Generic;
using System.Text;

namespace Watson
{
    /// <summary>
    /// Status for synchronous response objects.
    /// </summary>
    public enum SyncResponseStatus
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown,
        /// <summary>
        /// Success.
        /// </summary>
        Success,
        /// <summary>
        /// Failure to send.
        /// </summary>
        SendFailure,
        /// <summary>
        /// Failed.
        /// </summary>
        Failed,
        /// <summary>
        /// Request expired or response arrived too late.
        /// </summary>
        Expired,
        /// <summary>
        /// Unable to find the requested peer.
        /// </summary>
        PeerNotFound
    }
}
