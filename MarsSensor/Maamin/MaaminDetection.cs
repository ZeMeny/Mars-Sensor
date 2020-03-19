using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor.Maamin
{
    /// <summary>
    /// Class for IAF 'Maamin' System Detections
    /// </summary>
    public class MaaminDetection : DetectionBase
    {
        /// <summary>
        /// Gets or sets the Launch altitude
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Gets or sets the launch variance 
        /// </summary>
        public Variance Variance { get; set; }

        /// <summary>
        /// Gets or sets the launch speed
        /// </summary>
        public Velocity Velocity { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds from midnight 1970/1/1 to the last detection time
        /// </summary>
        public double DetectionTime { get; set; }

        /// <summary>
        /// Gets or sets if the system is tracking the launch
        /// </summary>
        public bool IsTracking { get; set; }

        /// <summary>
        /// Class Constructor
        /// </summary>
        public MaaminDetection()
        {
            Time = new DateTime(1970, 1, 1).AddMilliseconds(DetectionTime);
        }

        internal override IndicationType ToIndication(string sensorName = null, string detectionTypeName = null)
        {
            var type = new IndicationType
            {
                ID = ID.ToString(),
                CreationTime = new TimeType
                {
                    Value = DateTime.Now,
                    Zone = TimezoneType.GMT,
                },
                Item = new AerialTrackDetectionType
                {
                    TrackingStatusSpecified = true,
                    TrackingStatus = IsTracking ? TrackingStatusType.Tracking : TrackingStatusType.Undefined,
                    VectorDetectionSpeed = new VectorDetectionSpeed
                    {
                        Vx = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = Velocity.Vx,
                            },
                        },
                        Vy = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = Velocity.Vy,
                            },
                        },
                        Vz = new VelocityType
                        {
                            Item = new Speed
                            {
                                Units = SpeedUnitsType.MetersPerSecond,
                                Value = Velocity.Vz,
                            },
                        }
                    },
                    Location = new Point
                    {
                        Item = new LocationType
                        {
                            Item = new GeodeticLocation
                            {
                                Altitude = new AltitudeType
                                {
                                    Units = DistanceUnitsType.Meters,
                                    Value = Altitude,
                                    Reference = AltitudeReferenceType.AGL,
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
                            Value = Math.Sqrt(Math.Pow(Velocity.Vx, 2) + Math.Pow(Velocity.Vy, 2))
                        },
                    },
                    VarianceLocation = new VarianceLocation
                    {
                        Xx = new SensorStandard.MrsTypes.Variance(),
                        Xy = new SensorStandard.MrsTypes.Variance
                        {
                            Units = VarianceUnitsType.SqMeters,
                            Value = Variance.Xy,
                        },
                        Xz = new SensorStandard.MrsTypes.Variance
                        {
                            Units = VarianceUnitsType.SqMeters,
                            Value = Variance.Xz,
                        },
                        Yy = new SensorStandard.MrsTypes.Variance
                        {
                            Units = VarianceUnitsType.SqMeters,
                            Value = Variance.Yy
                        },
                        Yz = new SensorStandard.MrsTypes.Variance
                        {
                            Units = VarianceUnitsType.SqMeters,
                            Value = Variance.Yz
                        },
                        Zz = new SensorStandard.MrsTypes.Variance
                        {
                            Units = VarianceUnitsType.SqMeters,
                            Value = Variance.Zz
                        },
                    },
                    LastUpdatedDetectionTime = new TimeType
                    {
                        Value = Time,
                        Zone = TimezoneType.GMT,
                    },                    
                    ActivityTypeSpecified = false,
                    DetectionType = DetectionType.Balloon,
                    IdentificationFriendFoeSpecified = false,
                    IsFusedTrackSpecified = false,
                },
            };
            ((AerialTrackDetectionType) type.Item).VarianceLocation.Xx.Units = VarianceUnitsType.SqMeters;
            ((AerialTrackDetectionType)type.Item).VarianceLocation.Xx.Value = Variance.Xx;
            return type;
        }
    }
}
