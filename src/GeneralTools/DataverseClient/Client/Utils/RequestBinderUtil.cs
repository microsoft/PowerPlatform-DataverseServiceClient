// Ignore Spelling: Dataverse Utils

using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    internal static class RequestBinderUtil
    {
        internal static readonly string HEADERLIST = "HEADERLIST";
        /// <summary>
        /// Populates the request builder Info into message headers. 
        /// </summary>
        /// <param name="httpRequestMessageHeaders"></param>
        /// <param name="request"></param>
        internal static void ProcessRequestBinderProperties(HttpRequestMessageProperty httpRequestMessageHeaders, OrganizationRequest request)
        {
            foreach (var itm in request.Parameters)
            {
                if (itm.Key == Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID)
                {
                    AddorUpdateHeaderProperties(httpRequestMessageHeaders, Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID, itm.Value.ToString());
                    continue;
                }
                if (itm.Key == Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID)
                {
                    AddorUpdateHeaderProperties(httpRequestMessageHeaders, Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID, itm.Value.ToString());
                    continue;
                }
                if (itm.Key == HEADERLIST)
                {
                    if (itm.Value is Dictionary<string, string> hrdList)
                    {
                        foreach (var hdr in hrdList)
                        {
                            AddorUpdateHeaderProperties(httpRequestMessageHeaders, hdr.Key, hdr.Value);
                        }
                    }
                    continue;
                }
            }
            if ( request.Parameters.Count > 0 )
            {
                request.Parameters.Remove(Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID);
                request.Parameters.Remove(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID);
                request.Parameters.Remove(HEADERLIST);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="customHeaders"></param>
        /// <param name="request"></param>
        internal static void GetAdditionalHeaders(Dictionary<string, List<string>> customHeaders, OrganizationRequest request)
        {
            foreach (var itm in request.Parameters)
            {
                if (itm.Key == Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID)
                {
                    AddorUpdateHeaderProperties(customHeaders, Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID, itm.Value.ToString());
                    continue;
                }
                if (itm.Key == Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID)
                {
                    AddorUpdateHeaderProperties(customHeaders, Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID, itm.Value.ToString());
                    continue;
                }
                if (itm.Key == HEADERLIST)
                {
                    if (itm.Value is Dictionary<string, string> hrdList)
                    {
                        foreach (var hdr in hrdList)
                        {
                            AddorUpdateHeaderProperties(customHeaders, hdr.Key, hdr.Value);
                        }
                    }
                    continue;
                }
            }
            if (request.Parameters.Count > 0)
            {
                request.Parameters.Remove(Utilities.RequestHeaders.X_MS_CORRELATION_REQUEST_ID);
                request.Parameters.Remove(Utilities.RequestHeaders.X_MS_CLIENT_SESSION_ID);
                request.Parameters.Remove(HEADERLIST);
            }
        }

        /// <summary>
        /// Handle adding headers from request builder. 
        /// </summary>
        /// <param name="customHeaders"></param>
        /// <param name="hdrKey"></param>
        /// <param name="hrdValue"></param>
        private static void AddorUpdateHeaderProperties(Dictionary<string, List<string>> customHeaders, string hdrKey, string hrdValue)
        {
            if (customHeaders.Keys.Contains(hdrKey))
            {
                if (customHeaders[hdrKey] == null)
                    customHeaders[hdrKey] = new List<string>();

                customHeaders[hdrKey].Add(hrdValue);
            }
            else
            {
                customHeaders.Add(hdrKey, new List<string>() { hrdValue });
            }
        }

        private static void AddorUpdateHeaderProperties(HttpRequestMessageProperty httpRequestMessageHeaders, string key, string value)
        {
            if (httpRequestMessageHeaders.Headers.AllKeys.Contains(key))
            {
                httpRequestMessageHeaders.Headers.Add(key, value);
            }
            else
            {
                httpRequestMessageHeaders.Headers[key] = value;
            }
        }
    }
}
