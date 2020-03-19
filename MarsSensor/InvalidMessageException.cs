using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public object MarsMessage { get; }
        /// <summary>
        /// The message Type
        /// </summary>
        public MarsMessageTypes MessageType { get; }

        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="messageType">type of the invalid message</param>
        /// <param name="message">the message object</param>
        /// <param name="exceptionMessage">validation message</param>
        public InvalidMessageException(MarsMessageTypes messageType, object message, string exceptionMessage) : base(exceptionMessage)
        {
            MessageType = messageType;
            MarsMessage = message;
        }
    }
}
