//using Microsoft.Rest;
using Microsoft.PowerPlatform.Dataverse.Client.Exceptions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Creates a dataverse connection Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        public DataverseConnectionException(string message) 
            : base(message)
        {
        }

        /// <summary>
        /// Creates a dataverse connection Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="innerException">Supporting Exception</param>
        public DataverseConnectionException(string message, Exception innerException) 
            : base(message, innerException)
        {
            this.HResult = innerException.HResult;
        }

        /// <summary>
        /// Creates a dataverse connection Exception
        /// </summary>
        /// <param name="message">Error Message</param>
        /// <param name="errorCode">Error code</param>
        /// <param name="data">Data Properties</param>
        /// <param name="helpLink">Help Link</param>
        /// <param name="httpOperationException"></param>
        public DataverseConnectionException(string message, int errorCode, string helpLink, IDictionary<string, string> data, HttpOperationException httpOperationException = null)
            : base(message, httpOperationException)
        {
            HResult = errorCode;
            HelpLink = helpLink;
            Source = "Dataverse Server API";
            foreach (var itm in data)
            {
                this.Data.Add(itm.Key, itm.Value);
            }
        }

        /// <summary>
        /// Creates a Dataverse Connection Exception from an httpOperationError
        /// </summary>
        /// <param name="httpOperationException"></param>
        /// <returns></returns>
        public static DataverseConnectionException GenerateClientConnectionException(HttpOperationException httpOperationException)
        {
            string errorDetailPrefixString = "@Microsoft.PowerApps.CDS.ErrorDetails.";
            Dictionary<string, string> cdsErrorData = new Dictionary<string, string>();

            JToken ErrorBlock = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(httpOperationException.Response.Content))
                {
                    JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
                    ErrorBlock = contentBody["error"];
                }
            }
            catch { }

            if (ErrorBlock != null)
            {
                string errorMessage = DataverseTraceLogger.GetFirstLineFromString(ErrorBlock["message"]?.ToString()).Trim();
                var code = ErrorBlock["code"];
                int HResult = code != null && !string.IsNullOrWhiteSpace(code.ToString()) ? Convert.ToInt32(code.ToString(), 16) : -1;

                string HelpLink = ErrorBlock["@Microsoft.PowerApps.CDS.HelpLink"]?.ToString();

                foreach (var node in ErrorBlock.ToArray())
                {
                    if (node.Path.Contains(errorDetailPrefixString))
                    {
                        cdsErrorData.Add(node.Value<JProperty>().Name.ToString().Replace(errorDetailPrefixString, string.Empty), node.HasValues ? node.Value<JProperty>().Value.ToString() : string.Empty);
                    }
                }
                return new DataverseConnectionException(errorMessage, HResult, HelpLink, cdsErrorData, httpOperationException);
            }
            else
                return new DataverseConnectionException("Server Error, no error report generated from server", -1, string.Empty, cdsErrorData, httpOperationException);
        }
    }
}
