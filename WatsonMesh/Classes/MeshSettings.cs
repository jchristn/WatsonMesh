using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Watson
{
    public class MeshSettings
    {
        /// <summary>
        /// Indicate whether or not to automatically reconnect when a connection is severed.
        /// </summary>
        public bool AutomaticReconnect { get; set; }

        /// <summary>
        /// Reconnect attempt interval, in milliseconds.
        /// </summary>
        public int ReconnectIntervalMs { get; set; }

        /// <summary>
        /// Shared secret password to use to mutually authenticate mesh network members.
        /// </summary>
        public string SharedSecret { get; set; }
        
        /// <summary>
        /// Enable or disable acceptance of invalid or unverifiable SSL certificates.
        /// </summary>
        public bool AcceptInvalidCertificates { get; set; }

        /// <summary>
        /// Enable or disable mutual authentication when using SSL.
        /// </summary>
        public bool SslMutualAuthentication { get; set; }

        /// <summary>
        /// Enable or disable debugging for Watson networking operations.
        /// </summary>
        public bool DebugNetworking { get; set; }
         
        public MeshSettings()
        {
            AutomaticReconnect = true;
            ReconnectIntervalMs = 1000;
            SharedSecret = "default";
            AcceptInvalidCertificates = true;
            SslMutualAuthentication = false;
            DebugNetworking = false;
        }
    }
}
