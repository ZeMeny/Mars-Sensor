using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor.Shiran
{
    /// <summary>
    /// Class for 'shiran' system detections
    /// </summary>
    public class DroneDetection : DetectionBase
    {
        internal override IndicationType ToIndication(string sensorName = null, string detectionTypeName = null)
        {
            throw new NotImplementedException();
        }
    }
}
