using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;
using System;
using System.Collections.Generic;
using System.Net.Http;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Microsoft.PowerPlatform.Dataverse.Client.HttpUtils
{
    /// <summary>
    /// Wrapper around HttpRequestMessage type that copies properties of HttpRequestMessage so that
    /// they are available after the HttpClient gets disposed.
    /// </summary>
    public class HttpRequestMessageWrapper : HttpMessageWrapper
    {
        /// <summary>
        /// Initializes a new instance of the HttpRequestMessageWrapper class from HttpRequestMessage
        /// and content.
        /// </summary>
        public HttpRequestMessageWrapper(HttpRequestMessage httpRequest, string content)
        {
            if (httpRequest == null)
            {
                throw new ArgumentNullException("httpRequest");
            }

            CopyHeaders(httpRequest.Headers);
            CopyHeaders(httpRequest.GetContentHeaders());
            HttpRequestSanitizer.SanitizerHeaders(Headers);

            Content = content;
            Method = httpRequest.Method;
            RequestUri = httpRequest.RequestUri;
#pragma warning disable CS0618 // Options are only supported in .net 6
            if (httpRequest.Properties != null)
            {
                Properties = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> pair in httpRequest.Properties)
                {
                    Properties[pair.Key] = pair.Value;
                }
            }
#pragma warning restore CS0618
        }

        /// <summary>
        /// Gets or sets the HTTP method used by the HTTP request message.
        /// </summary>
        public HttpMethod Method { get; protected set; }

        /// <summary>
        /// Gets or sets the Uri used for the HTTP request.
        /// </summary>
        public Uri RequestUri { get; protected set; }

        /// <summary>
        /// Gets a set of properties for the HTTP request.
        /// </summary>
        public IDictionary<string, object> Properties { get; private set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
