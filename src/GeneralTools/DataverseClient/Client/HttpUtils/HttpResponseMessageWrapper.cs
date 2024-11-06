using System;
using System.Net.Http;
using System.Net;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Microsoft.PowerPlatform.Dataverse.Client.HttpUtils
{
    /// <summary>
    /// Wrapper around HttpResponseMessage type that copies properties of HttpResponseMessage so that
    /// they are available after the HttpClient gets disposed.
    /// </summary>
    public class HttpResponseMessageWrapper : HttpMessageWrapper
    {
        /// <summary>
        /// Initializes a new instance of the HttpResponseMessageWrapper class from HttpResponseMessage
        /// and content.
        /// </summary>
        public HttpResponseMessageWrapper(HttpResponseMessage httpResponse, string content)
        {
            if (httpResponse == null)
            {
                throw new ArgumentNullException("httpResponse");
            }

            CopyHeaders(httpResponse.Headers);
            CopyHeaders(httpResponse.GetContentHeaders());

            Content = content;
            StatusCode = httpResponse.StatusCode;
            ReasonPhrase = httpResponse.ReasonPhrase;
        }

        /// <summary>
        /// Gets or sets the status code of the HTTP response.
        /// </summary>
        public HttpStatusCode StatusCode { get; protected set; }

        /// <summary>
        /// Exposes the reason phrase, typically sent along with the status code. 
        /// </summary>
        public string ReasonPhrase { get; protected set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
