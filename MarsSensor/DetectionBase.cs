using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor
{
    /// <summary>
    /// Base class for sensor detections
    /// </summary>
    public abstract class DetectionBase
    {
        /// <summary>
        /// Gets or Sets the Detection's Time
        /// </summary>
        public DateTime Time { get; set; }
        /// <summary>
        /// Gets or Sets the Detection's Track ID
        /// </summary>
        public long ID { get; set; }
        /// <summary>
        /// Gets or Sets the Detection's Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the Detection longitude
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the Detection longitude
        /// </summary>
        public double Longitude { get; set; }


        internal abstract IndicationType ToIndication(string sensorName = null, string detectionTypeName = null);
    }
}
