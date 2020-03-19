using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor.Sigint
{
    /// <summary>
    /// Class for sisgint detections
    /// </summary>
    public class SigintDetection : DetectionBase
    {
        /// <summary>
        /// Gets or sets the detection's frequency
        /// </summary>
        public long Frequency { get; set; }

        /// <summary>
        /// Gets or sets the detection's End Time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the detection's Duration
        /// </summary>
        public TimeSpan Duration
        {
            get
            {
                return EndTime - Time;
            }
        }

        /// <summary>
        /// Gets or sets the detection's ellipse major radius
        /// </summary>
        public int SemiMajor { get; set; }

        /// <summary>
        /// Gets or sets the detection's ellipse minor radius
        /// </summary>
        public int SemiMinor { get; set; }

        /// <summary>
        /// Gets or sets the detection's azimuth
        /// </summary>
        public double Azimuth { get; set; }

        
        /// <summary>
        /// Class constructor
        /// </summary>
        public SigintDetection()
        {

        }

        internal override IndicationType ToIndication(string sensorName = null, string detectionTypeName = null)
        {
            TimeSpan delta = EndTime - Time;
            return new IndicationType
            {
                ID = ID.ToString(),
                CreationTime = new TimeType
                {
                    Value = DateTime.Now,
                    Zone = TimezoneType.GMT,
                },
                Item = new IntelligenceDetectionType
                {
                    DetectionStartTime = new TimeType
                    {
                        Value = DateTime.Now - delta,
                    },
                    Duration = new DeltaTime
                    {
                        Units = TimeUnitsType.Milliseconds,
                        Value = delta.TotalMilliseconds,
                    },
                    Accuracy = new Ellipse
                    {
                        Azimuth = new AzimuthType
                        {
                            Units = AngularUnitsType.Mils,
                            Value = Azimuth,
                        },
                        Center = new Point
                        {
                            Item = new LocationType
                            {
                                Item = new GeodeticLocation
                                {
                                    Datum = DatumType.WGS84,
                                    Longitude = new Longitude
                                    {
                                        Units = LatLonUnitsType.DecimalDegrees,
                                        Value = Longitude,
                                    },
                                    Latitude = new Latitude
                                    {
                                        Units = LatLonUnitsType.DecimalDegrees,
                                        Value = Latitude,
                                    },
                                },
                            }
                        },
                        Radius_V = new Distance
                        {
                            Units = DistanceUnitsType.Meters,
                            Value = SemiMajor,
                        },
                        Radius_H = new Distance
                        {
                            Units = DistanceUnitsType.Meters,
                            Value = SemiMinor,
                        },
                    },
                    Location = new []
                    {
                        new GeometricElement
                        {
                            Item = new Ellipse
                            {
                                Azimuth = new AzimuthType
                                {
                                    Units = AngularUnitsType.Mils,
                                    Value = Azimuth,
                                },
                                Center = new Point
                                {
                                    Item = new LocationType
                                    {
                                        Item = new GeodeticLocation
                                        {
                                            Datum = DatumType.WGS84,
                                            Longitude = new Longitude
                                            {
                                                Units = LatLonUnitsType.DecimalDegrees,
                                                Value = Longitude,
                                            },
                                            Latitude = new Latitude
                                            {
                                                Units = LatLonUnitsType.DecimalDegrees,
                                                Value = Longitude,
                                            },
                                        }
                                    }
                                },
                                Radius_V = new Distance
                                {
                                    Units = DistanceUnitsType.Meters,
                                    Value = SemiMajor,
                                },
                                Radius_H = new Distance
                                {
                                    Units = DistanceUnitsType.Meters,
                                    Value = SemiMinor,
                                },
                            }
                        }
                    },
                    SignalFrequency = new Frequency
                    {
                        Units = FrequencyUnitsType.MHz,
                        Value = (double)Frequency / 1000000,
                    },
                }
            };
        }
    }
}
