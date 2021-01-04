using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
{
    /// <summary>
    /// Used to encompass a ServiceClient Connection Centric exceptions
    /// </summary>
    [Serializable]
    public class CdsClientConnectionException : Exception
    {
        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        public CdsClientConnectionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="innerException">Supporting Exception</param>
        public CdsClientConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        protected CdsClientConnectionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
             : base(serializationInfo, streamingContext)
        {
        }
    }
}
