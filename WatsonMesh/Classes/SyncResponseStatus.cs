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
        Unknown,
        Success,
        SendFailure,
        Failed,
        Expired,
        PeerNotFound
    }
}
