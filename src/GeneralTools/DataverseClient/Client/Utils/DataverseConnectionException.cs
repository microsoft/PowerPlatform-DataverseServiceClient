using System;
using System.Runtime.Serialization;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    /// <summary>
    /// Used to encompass a ServiceClient Connection Centric exceptions
    /// </summary>
    [Serializable]
    public class DataverseConnectionException : Exception
    {
        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        public DataverseConnectionException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="innerException">Supporting Exception</param>
        public DataverseConnectionException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        protected DataverseConnectionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
             : base(serializationInfo, streamingContext)
        {
        }
    }
}
