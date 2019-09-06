using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 
using System.Runtime.Serialization;

namespace Watson
{
    /// <summary>
    /// The type of message.
    /// </summary> 
    internal enum MessageType
    { 
        Data, 
        Authentication, 
        Configuration, 
        Notification
    }
}
