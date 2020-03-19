using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarsSensor
{
    /// <summary>
    /// Message types in mars
    /// </summary>
    public enum MarsMessageTypes
    {
        /// <summary>
        /// Device Configuration message
        /// </summary>
        DeviceConfiguration,
        /// <summary>
        /// Device Subscription message
        /// </summary>
        DeviceSubscription,
        /// <summary>
        /// Device Status Report message
        /// </summary>
        DeviceStatusReport,
        /// <summary>
        /// Device Indication Report message
        /// </summary>
        DeviceIndicationReport,
        /// <summary>
        /// CommandMessage message
        /// </summary>
        CommandMessage,
    }
}
