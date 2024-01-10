namespace WatsonMesh
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Status for synchronous response objects.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SyncResponseStatusEnum
    {
        /// <summary>
        /// Unknow
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown,
        /// <summary>
        /// Success.
        /// </summary>
        [EnumMember(Value = "Success")]
        Success,
        /// <summary>
        /// Failure to send.
        /// </summary>
        [EnumMember(Value = "SendFailure")]
        SendFailure,
        /// <summary>
        /// Failed.
        /// </summary>
        [EnumMember(Value = "Failed")]
        Failed,
        /// <summary>
        /// Request expired or response arrived too late.
        /// </summary>
        [EnumMember(Value = "Expired")]
        Expired,
        /// <summary>
        /// Unable to find the requested peer.
        /// </summary>
        [EnumMember(Value = "PeerNotFound")]
        PeerNotFound
    }
}
