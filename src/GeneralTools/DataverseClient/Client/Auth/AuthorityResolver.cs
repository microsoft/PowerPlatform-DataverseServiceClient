using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth
{
    /// <summary>
    /// Details of expected authentication.
    /// </summary>
    public sealed class AuthenticationDetails
    {
        /// <summary>
        /// True if probing returned a WWW-Authenticate header.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// Authority to initiate OAuth flow with.
        /// </summary>
        // TODO: the 2 Uris here should be nullable: Uri? but that requires to update C# used for this solution from current 7.x to C# 9 or 10
        public Uri Authority { get; internal set; }

        /// <summary>
        /// OAuth resource to request authentication for.
        /// </summary>
        public Uri Resource { get; internal set; }

        /// <summary>
        /// Error message if probing failed.
        /// </summary>
        public string ErrorMessage { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Probes API endpoint to elicit a 401 response with the WWW-Authenticate header and processes the found information
    /// </summary>
    public sealed class AuthorityResolver
    {
        private const string AuthenticateHeader = "WWW-Authenticate";
        private const string Bearer = "bearer";
        private const string AuthorityKey = "authorization_uri";
        private const string ResourceKey = "resource_id";

        private readonly HttpClient _httpClient;
        private readonly Action<TraceEventType, string> _logger;

        /// <summary>
        /// instantiate resolver, using specified HttpClient to be used.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="logger"></param>
        public AuthorityResolver(HttpClient httpClient, Action<TraceEventType, string> logger = null)
        {
            _ = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Attempts to solicit a WWW-Authenticate reply using an unauthenticated GET call to the given endpoint.
        /// Parses returned header for details
        /// </summary>
        /// <param name="endpoint">endpoint to challenge for authority and resource</param>
        /// <param name="isOnPrem">if true, this is an OnPremsies server</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public async Task<AuthenticationDetails> ProbeForExpectedAuthentication(Uri endpoint, bool isOnPrem = false)
        {
            _ = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            var details = new AuthenticationDetails();

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                var errDetails = string.Empty;
                if (ex.InnerException is WebException wex)
                {
                    errDetails = $"; details: {wex.Message} ({wex.Status})";
                }

                details.ErrorMessage = $"Failed to get response from: {endpoint}; error: {ex.Message}{errDetails}";
                LogError(details.ErrorMessage);
                return details;
            }


            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest)
            {
                // didn't find endpoint.
                details.ErrorMessage = $"Failed to get Authority and Resource error. Attempt to Access Endpoint {endpoint} resulted in {response.StatusCode}.";
                LogError(details.ErrorMessage);
                return details;
            }

            if (response.Headers.Contains(AuthenticateHeader))
            {
                var authenticateHeaders = response.Headers.GetValues(AuthenticateHeader);
                // need to support OnPrem returning multiple Authentication headers. 
                foreach (var authenticateHeaderRaw in authenticateHeaders)
                {
                    if (details.Success)
                        break;

                    string authenticateHeader = authenticateHeaderRaw.Trim();

                    // This also checks for cases like "BearerXXXX authorization_uri=...." and "Bearer" and "Bearer "
                    if (!authenticateHeader.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)
                        || authenticateHeader.Length < Bearer.Length + 2
                        || !char.IsWhiteSpace(authenticateHeader[Bearer.Length]))
                    {
                        if (isOnPrem)
                            continue;

                        details.ErrorMessage = $"Malformed 'Bearer' format: {authenticateHeader}";
                        LogError(details.ErrorMessage);
                        return details;
                    }

                    authenticateHeader = authenticateHeader.Substring(Bearer.Length).Trim();

                    IDictionary<string, string> authenticateHeaderItems = null;
                    try
                    {
                        authenticateHeaderItems =
                            EncodingHelper.ParseKeyValueListStrict(authenticateHeader, ',', false, true);
                    }
                    catch (ArgumentException)
                    {
                        details.ErrorMessage = $"Malformed arguments in '{AuthenticateHeader}: {authenticateHeader}";
                        LogError(details.ErrorMessage);
                        return details;
                    }

                    if (authenticateHeaderItems != null)
                    {
                        if (!authenticateHeaderItems.TryGetValue(AuthorityKey, out var auth))
                        {
                            details.ErrorMessage = $"Response header from {endpoint} is missing expected key/value for {AuthorityKey}";
                            LogError(details.ErrorMessage);
                            return details;
                        }
                        details.Authority = new Uri(
                            auth.Replace("oauth2/authorize", "") // swap out the old oAuth pattern.
                            .Replace("common", "organizations")); // swap common for organizations because MSAL reasons.

                        if (!authenticateHeaderItems.TryGetValue(ResourceKey, out var res))
                        {
                            details.ErrorMessage = $"Response header from {endpoint} is missing expected key/value for {ResourceKey}";
                            LogError(details.ErrorMessage);
                            return details;
                        }
                        details.Resource = new Uri(res);
                        details.Success = true;
                    }
                }
            }
            return details;
        }

        private void LogError(string message)
        {
            if (_logger != null)
            {
                _logger(TraceEventType.Error, message);
            }
        }
    }
}
