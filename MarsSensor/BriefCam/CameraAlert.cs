using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor.BriefCam
{
    /// <summary>
    /// Class for 'BriefCam' systems alerts
    /// </summary>
    public class CameraAlert : DetectionBase
    {
        /// <summary>
        /// Gets or sets the person's name
        /// </summary>
        public string PersonName { get; set; }

        /// <summary>
        /// Gets os sets the detecting camera's name
        /// </summary>
        public string CameraName { get; set; }

        internal override IndicationType ToIndication(string sensorName = null, string detectionTypeName = null)
        {
            IndicationType indication = new IndicationType
            {
                ID = ID.ToString(),
                Description = Description,
                CreationTime = new TimeType
                {
                    Value = Time,
                    Zone = TimezoneType.GMT,
                },
                Item = new RadarTrackDetectionType
                {
                    LastUpdatedDetectionTime = new TimeType
                    {
                        Value = Time,
                        Zone = TimezoneType.GMT,
                    },
                    Direction = new AzimuthType
                    {
                        Units = AngularUnitsType.Mils,
                        Value = 0,
                    },
                    VelocityType = new VelocityType
                    {
                        Item = new Speed
                        {
                            Units = SpeedUnitsType.MetersPerSecond,
                            Value = 0,
                        }
                    },
                    Location = new Point
                    {
                        Item = new LocationType
                        {
                            Item = new GeodeticLocation
                            {
                                Datum = DatumType.WGS84,
                                Latitude = new Latitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = Latitude,
                                },
                                Longitude = new Longitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = Longitude,
                                }
                            }
                        }
                    },
                    DetectionTypeSpecified = detectionTypeName != null,
                }
            };
            RadarTrackDetectionType detection = indication.Item as RadarTrackDetectionType;
            if (detection.DetectionTypeSpecified)
            {
                detection.DetectionType = (DetectionType)Enum.Parse(typeof(DetectionType), detectionTypeName);
            }
            return indication;
        }
    }
}
