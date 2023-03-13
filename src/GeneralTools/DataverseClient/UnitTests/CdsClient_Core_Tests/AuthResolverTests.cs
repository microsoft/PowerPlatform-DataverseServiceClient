using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using Xunit;

namespace Client_Core_Tests.Auth
{
    public class AuthResolverTests
    {
        private const string AuthenticateHeader = "www-authenticate";
        private readonly MockHttpMessageHandler _msgHandler;
        private readonly HttpClient _httpClient;

        public AuthResolverTests()
        {
            _msgHandler = new MockHttpMessageHandler();
            _httpClient = new HttpClient(_msgHandler);
        }

        [Theory]
        [InlineData("https://login.somewhere.tst/abc/authorize", "https://hello.com", HttpStatusCode.Unauthorized, true, "Bearer authorization_uri=https://login.somewhere.tst/abc/authorize, resource_id=https://hello.com")]
        [InlineData("https://login.somewhere.tst/abc/authorize", "https://hello.com", HttpStatusCode.Unauthorized, true, "BeArEr AUTHORIZATION_URI=https://login.somewhere.tst/abc/authorize, reSoUrCe_ID=https://hello.com")]
        [InlineData("n/a", "n/a", HttpStatusCode.Unauthorized, false, "Bearer")]
        [InlineData("https://login.somewhere.tst/abc/authorize", "n/a", HttpStatusCode.Unauthorized, false, "Bearer authorization_uri=https://login.somewhere.tst/abc/authorize")]
        [InlineData("n/a", "https://hello.com", HttpStatusCode.Unauthorized, false, "Bearer  resource_id=https://hello.com")]
        public async Task ProbeSuccessful(string expectedAuthority, string expectedResource, HttpStatusCode expectedStatus, bool success, string responseHeader)
        {
            var endpoint = new Uri("https://ppdevtools.crm.dynamics.com/api/data/v9");
            var log = new List<string>();
            var resolver = new AuthorityResolver(_httpClient, (et, msg) =>
            {
                et.Should().Be(TraceEventType.Error);
                log.Add(msg);
            });

            _msgHandler.ResponseHeader = responseHeader;
            _msgHandler.ResponseStatus = expectedStatus;
            var details = await resolver.ProbeForExpectedAuthentication(endpoint).ConfigureAwait(false);

            details.Success.Should().Be(success);
            if (success)
            {
                details.Authority.Should().Be(new Uri(expectedAuthority));
                details.Resource.Should().Be(new Uri(expectedResource));
            }
            else
            {
                log.Count.Should().BeGreaterOrEqualTo(1);
            }
        }

        [Fact]
        public async void DnsErrorsHandled()
        {
            var endpoint = new Uri("https://doesnotexist-bad.crm.dynamics.com/api/data/v9");
            var log = new List<string>();
            var resolver = new AuthorityResolver(_httpClient, (et, msg) =>
            {
                et.Should().Be(TraceEventType.Error);
                log.Add(msg);
            });

            _msgHandler.ErrorOnSend = true;
            var details = await resolver.ProbeForExpectedAuthentication(endpoint).ConfigureAwait(false);
            details.Success.Should().BeFalse();
            log.Count.Should().BeGreaterOrEqualTo(1);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public string ResponseHeader { get; set; }
            public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.Unauthorized;
            public bool ErrorOnSend { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (ErrorOnSend)
                {
                    throw new HttpRequestException("Failed to get response", new WebException("The remote name could not be resolved", WebExceptionStatus.NameResolutionFailure));
                }

                var response = new HttpResponseMessage(ResponseStatus);
                response.Headers.Remove(AuthenticateHeader);
                response.Headers.TryAddWithoutValidation(AuthenticateHeader, ResponseHeader);

                return Task.FromResult(response);
            }
        }
    }
}
