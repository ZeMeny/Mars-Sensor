using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarsSensor.RADA
{
    /// <summary>
    /// Target types for RADA detections
    /// </summary>
    public enum RADATargetTypes
    {
        /// <summary>
        /// Unclassified detection
        /// </summary>
        Unclassified = 0,
        /// <summary>
        /// Unknown detection
        /// </summary>
        Unknown = 1,
        /// <summary>
        /// Aircraft detection
        /// </summary>
        Aircraft = 2,
    }
}
