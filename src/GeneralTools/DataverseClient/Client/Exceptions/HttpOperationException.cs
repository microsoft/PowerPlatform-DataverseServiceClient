using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.PowerPlatform.Dataverse.Client.HttpUtils;


namespace Microsoft.PowerPlatform.Dataverse.Client.Exceptions
{
    /// <summary>
    /// Http Exception wrapper class
    /// </summary>
    public class HttpOperationException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public HttpOperationException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public HttpOperationException(string message) : this(message, null)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public HttpOperationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        // Properties
        /// <summary>
        /// 
        /// </summary>
        public object Body { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public HttpRequestMessageWrapper Request { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public HttpResponseMessageWrapper Response { get; set; }


    }
}
