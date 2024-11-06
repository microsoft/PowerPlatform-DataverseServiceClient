using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.HttpUtils
{
    /// <summary>
    /// Sanitizer used internal by <see cref="HttpRequestMessageWrapper"/>.
    /// </summary>
    internal class HttpRequestSanitizer
    {
        private readonly static string _redactedPlaceholder = "REDACTED";
        private readonly static HashSet<string> _allowedHeaders = new HashSet<string>(new string[]
        {
            "x-ms-request-id",
            "x-ms-client-request-id",
            "x-ms-return-client-request-id",
            "traceparent",
            "MS-CV",

            "Accept",
            "Cache-Control",
            "Connection",
            "Content-Length",
            "Content-Type",
            "Date",
            "ETag",
            "Expires",
            "If-Match",
            "If-Modified-Since",
            "If-None-Match",
            "If-Unmodified-Since",
            "Last-Modified",
            "Pragma",
            "Request-Id",
            "Retry-After",
            "Server",
            "Transfer-Encoding",
            "User-Agent",
            "WWW-Authenticate" // OAuth Challenge header.
            }, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Sanitize value of sensitive headers in the given <paramref name="headers"/>.
        /// </summary>
        /// <param name="headers">A collection of headers to sanitize.</param>
        public static void SanitizerHeaders(IDictionary<string, IEnumerable<string>> headers)
        {
            if (headers == null)
            {
                return;
            }

            var namesOfHeaderToSanitize = headers.Keys.Except(_allowedHeaders, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (string name in namesOfHeaderToSanitize)
            {
                headers[name] = new string[] { _redactedPlaceholder };
            }
        }
    }
}
