﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensorStandard;

namespace MarsSensor
{
    /// <summary>
    /// An Exception related to mars messages validation
    /// </summary>
    public class InvalidMessageException : Exception
    {
        /// <summary>
        /// The message object
        /// </summary>
        public MrsMessage MarsMessage { get; }

        /// <summary>
        /// Class Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public InvalidMessageException(MrsMessage message, Exception innerException) : base(innerException.Message, innerException)
        {
            MarsMessage = message;
        }
    }
}
