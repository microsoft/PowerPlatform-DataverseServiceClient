using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
{
    [Serializable]
    /// Used to encompas a ServiceClient execption 
    public class CdsConnectionException : Exception
    {
        public CdsConnectionException(string message) 
            : base(message)
        {
        }

        public CdsConnectionException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        protected CdsConnectionException(SerializationInfo serializationInfo, StreamingContext streamingContext)
             : base(serializationInfo, streamingContext)
        {
        }
    }
}
