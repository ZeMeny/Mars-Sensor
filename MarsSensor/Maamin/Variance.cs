using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarsSensor.Maamin
{
    /// <summary>
    /// Maamin Detection varinace location
    /// </summary>
    public class Variance
    {
        /// <summary>
        /// Variance X
        /// </summary>
        public double Xx { get; set; }

        /// <summary>
        /// Variance Y
        /// </summary>
        public double Yy { get; set; }

        /// <summary>
        /// Variance Z
        /// </summary>
        public double Zz { get; set; }

        /// <summary>
        /// Variance VX
        /// </summary>
        public double Xy { get; set; }

        /// <summary>
        /// Variance VY
        /// </summary>
        public double Xz { get; set; }

        /// <summary>
        /// Variance VZ
        /// </summary>
        public double Yz { get; set; }
    }
}
