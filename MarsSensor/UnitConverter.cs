using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;
using SensorStandard.MrsTypes;

namespace MarsSensor
{
    internal class UnitConverter
    {
        public static double DegreeToMils(double degree)
        {
            return degree * (6400 / 360);
        }

        public static double RadianToDegree(double rad)
        {
            return ((rad / Math.PI * 180) + 360) % 360;
        }

        public static double DegreeToRadian(double degree)
        {
            return degree * Math.PI / 180;
        }

        public static double RadianToMils(double rad)
        {
            return rad * (6400 / (2 * Math.PI));
        }

        public static System.Windows.Point LocationToPoint(Point point)
        {
            if (point.Item is LocationType locationType)
            {
                return LocationToPoint(locationType);
            }
            
            throw new ArgumentException("Not a location type");
        }

        public static System.Windows.Point LocationToPoint(LocationType location)
        {
            if (location.Item is GeodeticLocation geodeticLcation)
            {
                var x = geodeticLcation.Latitude.Value;
                var y = geodeticLcation.Longitude.Value;
                return new System.Windows.Point(x, y);
            }

            throw new ArgumentException("Not a geodetic location");
        }
    }
}
