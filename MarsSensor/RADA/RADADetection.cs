using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor.RADA
{
    /// <summary>
    /// Class for RADA system detections
    /// </summary>
    public class RADADetection : DetectionBase
    {
        /// <summary>
        /// Gets or sets the Detection's Altitude (Meters above MSL)
        /// </summary>
        public double Altitude { get; set; }
        /// <summary>
        /// Gets or sets the Detection's Velocity X (Meters/second)
        /// </summary>
        public double VelocityX { get; set; }
        /// <summary>
        /// Gets or sets the Detection's Velocity Y  (Meters/second)
        /// </summary>
        public double VelocityY { get; set; }
        /// <summary>
        /// Gets or sets the Detection's Velocity Z  (Meters/second)
        /// </summary>
        public double VelocityZ { get; set; }
        /// <summary>
        /// Gets or sets the Detection's Target type
        /// </summary>
        public RADATargetTypes Type { get; set; }

        internal override IndicationType ToIndication(string sensorName = null, string detectionTypeName = null)
        {
            IndicationType indication = new IndicationType
            {
                ID = ID.ToString(),
                CreationTime = new TimeType
                {
                    Value = DateTime.Now,
                    Zone = TimezoneType.GMT,
                },
                Item = new AerialTrackDetectionType
                {
                    TrackingStatusSpecified = false,
                    Location = new Point
                    {
                        Item = new LocationType
                        {
                            Item = new GeodeticLocation
                            {
                                Altitude = new AltitudeType
                                {
                                    Units = DistanceUnitsType.Meters,
                                    Reference = AltitudeReferenceType.MSL,
                                    Value = Altitude,
                                },
                                Longitude = new Longitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = UnitConverter.RadianToDegree(Longitude),
                                },
                                Latitude = new Latitude
                                {
                                    Units = LatLonUnitsType.DecimalDegrees,
                                    Value = UnitConverter.RadianToDegree(Latitude),
                                },
                            },
                        },
                    },
                    VelocityType = new VelocityType
                    {
                        Item = new Speed
                        {
                            Units = SpeedUnitsType.MetersPerSecond,
                            Value = Math.Sqrt(Math.Pow(VelocityX, 2) +
                                                      Math.Pow(VelocityY, 2))
                        },
                    },
                    LastUpdatedDetectionTime = new TimeType
                    {
                        Value = Time,
                        Zone = TimezoneType.GMT,
                    },
                    ActivityTypeSpecified = false,
                    IdentificationFriendFoeSpecified = false,
                    IsFusedTrackSpecified = false,
                    VectorDetectionSpeed = new VectorDetectionSpeed
                    {
                        Vx = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = VelocityX
                            }
                        },
                        Vy = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = VelocityY
                            }
                        },
                        Vz = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = VelocityZ
                            }
                        },
                    }
                },
            };
            var indicationItem = (AerialTrackDetectionType)indication.Item;
            if (Type == RADATargetTypes.Aircraft)
            {
                indicationItem.DetectionTypeSpecified = true;
                indicationItem.DetectionType = DetectionType.Aircraft;
            }
            else
            {
                indicationItem.DetectionTypeSpecified = false;
            }
            return indication;
        }
    }
}
