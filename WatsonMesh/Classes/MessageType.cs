using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

namespace Watson
{
    /// <summary>
    /// The type of message.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MessageType
    {
        [EnumMember(Value = "Data")]
        Data,
        [EnumMember(Value = "Authentication")]
        Authentication,
        [EnumMember(Value = "Configuration")]
        Configuration,
        [EnumMember(Value = "Notification")]
        Notification
    }
}
