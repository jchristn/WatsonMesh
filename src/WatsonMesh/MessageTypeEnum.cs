namespace WatsonMesh
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The type of message.
    /// </summary> 
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageTypeEnum
    { 
        /// <summary>
        /// Application data.
        /// </summary>
        [EnumMember(Value = "Data")]
        Data 
    }
}
