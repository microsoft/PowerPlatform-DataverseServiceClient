using Microsoft.Rest;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    /// <summary>
    /// Used to encompass a ServiceClient Operation Exception 
    /// </summary>
    [Serializable]
    public class DataverseOperationException : Exception
    {
        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        public DataverseOperationException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="errorCode">Error code</param>
        /// <param name="data">Data Properties</param>
        /// <param name="helpLink">Help Link</param>
        public DataverseOperationException(string message, int errorCode , string helpLink , IDictionary<string,string> data)
            : base(message)
        {
            HResult = errorCode;
            HelpLink = helpLink;
            Source = "Cds Server API";
            foreach (var itm in data)
            {
                this.Data.Add(itm.Key, itm.Value);
            }
        }

        /// <summary>
        /// Creates a CdsService Client Exception from a httpOperationResult. 
        /// </summary>
        /// <param name="httpOperationException"></param>
        public static DataverseOperationException GenerateClientOperationException(HttpOperationException httpOperationException )
        {
            string errorDetailPrefixString = "@Microsoft.PowerApps.CDS.ErrorDetails.";
            Dictionary<string, string> cdsErrorData = new Dictionary<string, string>();

            JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
            var ErrorBlock = contentBody["error"];
            if (ErrorBlock != null)
            {
                string errorMessage = DataverseTraceLogger.GetFirstLineFromString(ErrorBlock["message"]?.ToString()).Trim();
                int HResult = ErrorBlock["code"] != null ? Convert.ToInt32(ErrorBlock["code"].ToString(), 16) : -1;
                string HelpLink = ErrorBlock["@Microsoft.PowerApps.CDS.HelpLink"]?.ToString();

                foreach (var node in ErrorBlock.ToArray())
                {
                    if (node.Path.Contains(errorDetailPrefixString))
                    {
                        cdsErrorData.Add(node.Value<JProperty>().Name.ToString().Replace(errorDetailPrefixString, string.Empty), node.HasValues ? node.Value<JProperty>().Value.ToString() : string.Empty);
                    }
                }
                return new DataverseOperationException(errorMessage, HResult, HelpLink, cdsErrorData);
            }
            else
                return new DataverseOperationException("Server Error, no error report generated from server" , -1 , string.Empty, cdsErrorData);

        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="innerException">Supporting Exception</param>
        public DataverseOperationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Creates a CdsService Client Exception
        /// </summary>
        /// <param name="serializationInfo"></param>
        /// <param name="streamingContext"></param>
        protected DataverseOperationException(SerializationInfo serializationInfo, StreamingContext streamingContext)
             : base(serializationInfo, streamingContext)
        {
        }
    }
}
