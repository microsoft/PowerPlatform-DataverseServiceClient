using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.PowerPlatform.Dataverse.Client.Connector;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Xunit;

namespace Client_Core_UnitTests
{
    [CollectionDefinition("Workload attribution", DisableParallelization = true)]
    public sealed class WorkloadAttributionCollection { }

    [Collection("Workload attribution")]
    public class WorkloadAttributionTests
    {
        private static readonly Guid OriginId = Guid.Parse("c032e58e-8af5-4a4d-aed7-1c4e84367c25");
        private static readonly Guid ScopeId = Guid.Parse("91fc5b98-1b8d-4b15-8796-b8a54a6a32a1");

        private static WorkloadAttribution Workload(string component = null) =>
            new WorkloadAttribution("contoso_catalog", OriginId, component: component);

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void SerializesOptionalCombinations(bool scope, bool component)
        {
            var value = new WorkloadAttribution("contoso_catalog", OriginId,
                scope ? "catalog_reader" : null, scope ? ScopeId : (Guid?)null, component ? "search" : null);
            Assert.StartsWith($"origin.name=contoso_catalog, origin.id={OriginId:D}", value.ToString());
            Assert.Equal(scope, value.ToString().Contains("scope.name=catalog_reader, scope.id="));
            Assert.Equal(component, value.ToString().Contains("component=search"));
            Assert.DoesNotContain("unknown", value.ToString());
            Assert.DoesNotContain("undefined", value.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData("1abc")]
        [InlineData("bad-name")]
        [InlineData("bad name")]
        [InlineData("bad\n")]
        [InlineData("bad\r")]
        [InlineData("bad,name")]
        [InlineData("bad=name")]
        [InlineData("\u00e9rp")]
        public void RejectsInvalidNamesAtConstruction(string name)
        {
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution(name, OriginId));
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution("origin", OriginId, name, ScopeId));
            Assert.Throws<ArgumentException>(() => Workload(name));
        }

        [Fact]
        public void ValidatesIdsPairingAndExactMaximumSize()
        {
            Assert.Throws<ArgumentNullException>(() => new WorkloadAttribution(null, OriginId));
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution("origin", Guid.Empty));
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution("origin", OriginId, "scope"));
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution("origin", OriginId, scopeId: ScopeId));
            Assert.Throws<ArgumentException>(() => new WorkloadAttribution("origin", OriginId, "scope", Guid.Empty));
            Assert.Throws<ArgumentException>(() => Workload(new string('a', 64)));
            var largest = new WorkloadAttribution(new string('a', 63), OriginId, new string('b', 63), ScopeId, new string('c', 63));
            Assert.Equal(321, Encoding.UTF8.GetByteCount(largest.ToString()));
            Assert.Contains("origin.name=abc", new WorkloadAttribution("abc", OriginId).ToString());
        }

        [Fact]
        public void ConnectionOptionsAreCapturedWithoutConnecting()
        {
            var workload = Workload();
            var options = new ConnectionOptions { WorkloadAttribution = workload };
            using (var client = new ServiceClient(options, deferConnection: true))
            {
                options.WorkloadAttribution = null;
                Assert.Same(workload, client.WorkloadAttribution);
                Assert.False(client.IsReady);
            }
        }

        [Fact]
        public async Task BuilderIsOneShotAndSupportsExplicitOmission()
        {
            using (var client = CreateMockClient(out var service))
            {
                var observed = new List<WorkloadAttribution>();
                service.Setup(s => s.Execute(It.IsAny<OrganizationRequest>())).Returns((OrganizationRequest request) =>
                {
                    Assert.False(request.Parameters.Contains(WorkloadAttributionScope.ParameterName));
                    observed.Add(WorkloadAttributionScope.Current);
                    return new OrganizationResponse();
                });
                client.WorkloadAttribution = Workload("default_component");
                var requestWorkload = Workload("request_component");
                var builder = client.CreateRequestBuilder().WithWorkloadAttribution(requestWorkload);
                await builder.ExecuteAsync(new WhoAmIRequest());
                await builder.ExecuteAsync(new WhoAmIRequest());
                await builder.WithWorkloadAttribution(null).ExecuteAsync(new WhoAmIRequest());
                await client.ExecuteAsync(new WhoAmIRequest());
                Assert.Equal(new[] { requestWorkload, client.WorkloadAttribution, null, client.WorkloadAttribution }, observed);
                Assert.Null(WorkloadAttributionScope.Current);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DirectWebApiUsesDefaultsAndDoesNotMutateCallerHeaders(bool async)
        {
            using (var client = CreateMockClient(out _))
            {
                var workload = Workload("direct_call");
                client.WorkloadAttribution = workload;
                var sent = new List<string>();
                client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(request =>
                {
                    sent.Add(request.Headers.GetValues("x-ms-workload").Single());
                    Assert.Equal("preserved", request.Headers.GetValues("x-test").Single());
                    Assert.Equal("Bearer supplied", request.Headers.GetValues("Authorization").Single());
                    Assert.Equal("4.0", request.Headers.GetValues("OData-Version").Single());
                    return Task.FromResult(Ok());
                }));
                client._connectionSvc.RequestAdditionalHeadersAsync = () => Task.FromResult(new Dictionary<string, string>
                {
                    ["X-MS-WORKLOAD"] = "callback_raw",
                    ["x-test"] = "preserved"
                });
                var headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X-Ms-Workload"] = new List<string> { "request_raw" },
                    ["authorization"] = new List<string> { "Bearer supplied" },
                    ["odata-version"] = new List<string> { "3.0" }
                };
                using (var response = async
                    ? await client.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, headers)
                    : client.ExecuteWebRequest(HttpMethod.Get, "WhoAmI", null, headers))
                    Assert.True(response.IsSuccessStatusCode);
                Assert.Equal(new[] { workload.ToString() }, sent);
                Assert.Equal(3, headers.Count);
                Assert.Equal("request_raw", headers["X-Ms-Workload"].Single());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TranslatedWebApiCapturesRequestOverrideAcrossRetry(bool async)
        {
            using (var client = CreateMockClient(out _))
            {
                bool previousUseWebApi = client.UseWebApi;
                try
                {
                    client.UseWebApi = true;
                    client.WorkloadAttribution = Workload("client_default");
                    var workload = Workload("request_override");
                    var observed = new List<string>();
                    client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(httpRequest =>
                    {
                        observed.Add(httpRequest.Headers.GetValues("x-ms-workload").Single());
                        client.WorkloadAttribution = Workload("changed_default");
                        return Task.FromResult(observed.Count == 1 ? TransientFailure() : Ok());
                    }));
                    var builder = client.CreateRequestBuilder()
                        .WithHeader("X-MS-WORKLOAD", "raw_header")
                        .WithWorkloadAttribution(workload);
                    var request = new DeleteRequest { Target = new EntityReference("account", Guid.NewGuid()) };
                    if (async)
                        await builder.ExecuteAsync(request);
                    else
                        builder.Execute(request);
                    Assert.Equal(new[] { workload.ToString(), workload.ToString() }, observed);
                    Assert.False(request.Parameters.Contains(WorkloadAttributionScope.ParameterName));
                    Assert.Null(WorkloadAttributionScope.Current);
                }
                finally
                {
                    client.UseWebApi = previousUseWebApi;
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ColdMetadataRequestsPreserveOverrideOrExplicitOmission(bool omit)
        {
            using (var client = CreateMockClient(out var service))
            using (var independent = CreateMockClient(out var otherService))
            {
                bool previousUseWebApi = client.UseWebApi;
                try
                {
                    client.UseWebApi = true;
                    client.WorkloadAttribution = Workload("client_default");
                    independent.WorkloadAttribution = Workload("independent_call");
                    var selected = omit ? null : Workload("initiating_call");
                    var observed = new List<WorkloadAttribution>();
                    otherService.Setup(s => s.Execute(It.IsAny<WhoAmIRequest>())).Returns(() =>
                    {
                        Assert.Same(independent.WorkloadAttribution, WorkloadAttributionScope.Current);
                        return new WhoAmIResponse();
                    });
                    service.Setup(s => s.Execute(It.IsAny<RetrieveEntityRequest>())).Returns(() =>
                    {
                        observed.Add(WorkloadAttributionScope.Current);
                        independent.Execute(new WhoAmIRequest());
                        Assert.Same(selected, WorkloadAttributionScope.Current);
                        var response = new RetrieveEntityResponse();
                        var metadata = new Microsoft.Xrm.Sdk.Metadata.EntityMetadata
                        {
                            LogicalName = "account", EntitySetName = "accounts"
                        };
                        metadata.GetType().GetProperty("PrimaryIdAttribute").SetValue(metadata, "accountid", null);
                        response.Results["EntityMetadata"] = metadata;
                        return response;
                    });
                    client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(httpRequest =>
                    {
                        observed.Add(WorkloadAttributionScope.Current);
                        Assert.Equal(selected?.ToString(),
                            httpRequest.Headers.TryGetValues("x-ms-workload", out var values) ? values.Single() : null);
                        return Task.FromResult(Ok());
                    }));
                    await client.CreateRequestBuilder().WithWorkloadAttribution(selected).ExecuteAsync(
                        new DeleteRequest { Target = new EntityReference("account", Guid.NewGuid()) });
                    // This minimal fixture triggers relationship and full-metadata lookups before the HTTP send.
                    service.Verify(s => s.Execute(It.IsAny<RetrieveEntityRequest>()), Times.Exactly(2));
                    Assert.Equal(new[] { selected, selected, selected }, observed);
                    Assert.Null(WorkloadAttributionScope.Current);
                }
                finally
                {
                    client.UseWebApi = previousUseWebApi;
                }
            }
        }

        [Fact]
        public async Task ConcurrentClientsSharingConnectionDoNotShareWorkloads()
        {
            using (var first = CreateMockClient(out _))
            using (var second = CreateMockClient(out _))
            {
                second._connectionSvc.Dispose();
                second._connectionSvc = first._connectionSvc;
                first.WorkloadAttribution = Workload("first_client");
                second.WorkloadAttribution = Workload("second_client");
                var firstValue = first.WorkloadAttribution.ToString();
                var secondValue = second.WorkloadAttribution.ToString();
                var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var observed = new ConcurrentQueue<string>();
                int count = 0;
                first._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(async request =>
                {
                    observed.Enqueue(request.Headers.TryGetValues("x-ms-workload", out var values) ? values.Single() : null);
                    if (Interlocked.Increment(ref count) == 1)
                    {
                        entered.SetResult(true);
                        await release.Task;
                    }
                    return Ok();
                }));
                var pending = first.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, null);
                try
                {
                    Assert.Same(entered.Task, await Task.WhenAny(entered.Task, Task.Delay(10000)));
                    first.WorkloadAttribution = null;
                    using (await second.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, null)) { }
                    using (await first.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, null)) { }
                }
                finally
                {
                    release.TrySetResult(true);
                    using (await pending) { }
                }
                Assert.Equal(new[] { firstValue, secondValue, null }, observed.ToArray());
                Assert.Null(WorkloadAttributionScope.Current);
            }
        }

        [Fact]
        public async Task WebApiBatchAttributesOnlyOuterRequestAndRawHeadersStillWork()
        {
            using (var client = CreateMockClient(out _))
            {
                var observed = new List<string>();
                const string body = "--batch_test\r\nContent-Type: application/http\r\n\r\nGET accounts HTTP/1.1\r\n\r\n--batch_test--";
                client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(async request =>
                {
                    observed.Add(request.Headers.TryGetValues("x-ms-workload", out var values) ? values.Single() : null);
                    Assert.Equal(body, await request.Content.ReadAsStringAsync());
                    return Ok();
                }));
                client.WorkloadAttribution = Workload("batch");
                using (await client.ExecuteWebRequestAsync(HttpMethod.Post, "$batch", body, null, "multipart/mixed;boundary=batch_test")) { }
                client.WorkloadAttribution = null;
                var raw = new Dictionary<string, List<string>> { ["x-ms-workload"] = new List<string> { "raw_unchanged" } };
                using (await client.ExecuteWebRequestAsync(HttpMethod.Post, "$batch", body, raw, "multipart/mixed;boundary=batch_test")) { }
                Assert.Equal(new[] { Workload("batch").ToString(), "raw_unchanged" }, observed);
            }
        }

        [Fact]
        public async Task FailedAndCanceledCallsRestoreAttribution()
        {
            using (var client = CreateMockClient(out _))
            {
                client.WorkloadAttribution = Workload("failing_call");
                client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(request =>
                {
                    Assert.Same(client.WorkloadAttribution, WorkloadAttributionScope.Current);
                    throw new OperationCanceledException("test cancellation");
                }));
                await Assert.ThrowsAsync<OperationCanceledException>(() =>
                    client.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, null));
                Assert.Null(WorkloadAttributionScope.Current);
                client._connectionSvc.WebApiHttpClient = new HttpClient(new CaptureHandler(request =>
                {
                    throw new InvalidOperationException("test failure");
                }));
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    client.ExecuteWebRequestAsync(HttpMethod.Get, "WhoAmI", null, null));
                Assert.Null(WorkloadAttributionScope.Current);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SoapWirePreservesTypedOverrideAndHeaderPrecedenceAcrossRetry(bool async)
        {
            using (var server = new SoapServer(3, throttleFirst: true))
            using (var proxy = new OrganizationWebProxyClientAsync(server.Endpoint, false) { HeaderToken = "local-test-only" })
            using (var client = new ServiceClient(proxy, sourceOrgVersion: new Version(9, 2)))
            {
                client.WorkloadAttribution = Workload("client_default");
                var workload = Workload("soap_override");
                client._connectionSvc.RequestAdditionalHeadersAsync = () => Task.FromResult(new Dictionary<string, string>
                {
                    ["X-MS-WORKLOAD"] = "callback_raw",
                    ["x-test"] = "preserved"
                });
                var builder = client.CreateRequestBuilder()
                    .WithHeader("x-ms-workload", "builder_raw")
                    .WithWorkloadAttribution(workload);
                if (async)
                    await builder.ExecuteAsync(new WhoAmIRequest());
                else
                    builder.Execute(new WhoAmIRequest());
                client.WorkloadAttribution = null;
                client._connectionSvc.RequestAdditionalHeadersAsync = null;
                if (async)
                    await client.ExecuteAsync(new WhoAmIRequest());
                else
                    client.Execute(new WhoAmIRequest());
                await server.Completion;
                var calls = server.Calls.ToArray();
                Assert.Equal(3, calls.Length);
                Assert.Equal(workload.ToString(), calls[0].Headers["x-ms-workload"]);
                Assert.Equal(workload.ToString(), calls[1].Headers["x-ms-workload"]);
                Assert.Equal("preserved", calls[1].Headers["x-test"]);
                Assert.False(calls[2].Headers.ContainsKey("x-ms-workload"));
                Assert.All(calls, call => Assert.DoesNotContain(WorkloadAttributionScope.ParameterName, call.Body));
                Assert.Null(WorkloadAttributionScope.Current);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SoapBatchAndCloneUseTheirOwnCapturedDefaults(bool transaction)
        {
            using (var server = new SoapServer(2))
            using (var proxy = new OrganizationWebProxyClientAsync(server.Endpoint, false) { HeaderToken = "local-test-only" })
            using (var client = new ServiceClient(proxy, sourceOrgVersion: new Version(9, 2)))
            {
                client.WorkloadAttribution = Workload("inherited_default");
                client._connectionSvc.CurrentUser = new WhoAmIResponse();
                using (var clone = client.Clone())
                {
                    Assert.Same(client.WorkloadAttribution, clone.WorkloadAttribution);
                    client.WorkloadAttribution = Workload("changed_parent");
                    var requests = new OrganizationRequestCollection { new WhoAmIRequest() };
                    OrganizationRequest batch = transaction
                        ? (OrganizationRequest)new ExecuteTransactionRequest { Requests = requests }
                        : new ExecuteMultipleRequest { Requests = requests, Settings = new ExecuteMultipleSettings() };
                    await clone.ExecuteAsync(batch);
                    await client.ExecuteAsync(new WhoAmIRequest());
                    await server.Completion;
                    var calls = server.Calls.ToArray();
                    Assert.Equal(Workload("inherited_default").ToString(), calls[0].Headers["x-ms-workload"]);
                    Assert.Equal(Workload("changed_parent").ToString(), calls[1].Headers["x-ms-workload"]);
                    Assert.DoesNotContain(WorkloadAttributionScope.ParameterName, calls[0].Body);
                    Assert.DoesNotContain("x-ms-workload", calls[0].Body);
                }
            }
        }

        private static ServiceClient CreateMockClient(out Mock<IOrganizationService> service)
        {
            new TestSupport().SetupMockAndSupport(out service, out _, out var client);
            return client;
        }

        private static HttpResponseMessage Ok() =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        private static HttpResponseMessage TransientFailure()
        {
            var result = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("{\"error\":{\"code\":\"0x80072322\",\"message\":\"test gateway failure\"}}")
            };
            result.Headers.Add("Retry-After", "0");
            return result;
        }

        private sealed class CaptureHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _send;
            internal CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) { _send = send; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _send(request);
        }

        private sealed class SoapCall
        {
            internal Dictionary<string, string> Headers { get; set; }
            internal string Body { get; set; }
        }

        private sealed class SoapServer : IDisposable
        {
            private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource _timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            private readonly CancellationTokenRegistration _stopOnTimeout;
            internal ConcurrentQueue<SoapCall> Calls { get; } = new ConcurrentQueue<SoapCall>();
            internal Uri Endpoint { get; }
            internal Task Completion { get; }

            internal SoapServer(int count, bool throttleFirst = false)
            {
                _listener.Start();
                _stopOnTimeout = _timeout.Token.Register(() => _listener.Stop());
                Endpoint = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/Organization.svc/web");
                Completion = Task.Run(async () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        using (var connection = await _listener.AcceptTcpClientAsync())
                        using (var stream = connection.GetStream())
                        {
                            var headerBytes = new List<byte>();
                            var one = new byte[1];
                            while (true)
                            {
                                if (await stream.ReadAsync(one, 0, 1, _timeout.Token) == 0)
                                    throw new IOException("Incomplete SOAP HTTP headers.");
                                headerBytes.Add(one[0]);
                                if (headerBytes.Count > 65536)
                                    throw new IOException("SOAP test headers exceeded the limit.");
                                if (headerBytes.Count >= 4 && Encoding.ASCII.GetString(headerBytes.Skip(headerBytes.Count - 4).ToArray()) == "\r\n\r\n")
                                    break;
                            }
                            var lines = Encoding.ASCII.GetString(headerBytes.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                            var headers = lines.Skip(1).ToDictionary(line => line.Substring(0, line.IndexOf(':')),
                                line => line.Substring(line.IndexOf(':') + 1).Trim(), StringComparer.OrdinalIgnoreCase);
                            if (headers.TryGetValue("Expect", out string expect) && expect.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                byte[] interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                                await stream.WriteAsync(interim, 0, interim.Length, _timeout.Token);
                            }
                            var body = new byte[int.Parse(headers["Content-Length"])];
                            int offset = 0;
                            while (offset < body.Length)
                            {
                                int read = await stream.ReadAsync(body, offset, body.Length - offset, _timeout.Token);
                                if (read == 0)
                                    throw new IOException("Incomplete SOAP HTTP body.");
                                offset += read;
                            }
                            string xml = Encoding.UTF8.GetString(body);
                            Calls.Enqueue(new SoapCall { Headers = headers, Body = xml });
                            bool throttle = throttleFirst && i == 0;
                            string response;
                            if (throttle)
                            {
                                var fault = new OrganizationServiceFault { ErrorCode = ErrorCodes.ThrottlingBurstRequestLimitExceededError, Message = "test throttle" };
                                fault.ErrorDetails["Retry-After"] = TimeSpan.Zero;
                                var faultXml = new StringBuilder();
                                using (var writer = XmlWriter.Create(faultXml, new XmlWriterSettings { OmitXmlDeclaration = true }))
                                    new DataContractSerializer(typeof(OrganizationServiceFault)).WriteObject(writer, fault);
                                response = $"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><s:Fault><faultcode>s:Server</faultcode><faultstring xml:lang=\"en-US\">test throttle</faultstring><detail>{faultXml}</detail></s:Fault></s:Body></s:Envelope>";
                            }
                            else
                            {
                                XNamespace services = "http://schemas.microsoft.com/xrm/2011/Contracts/Services";
                                string name = XDocument.Parse(xml).Descendants().First(element => element.Name.LocalName == "RequestName").Value;
                                var result = new StringBuilder();
                                using (var writer = XmlWriter.Create(result, new XmlWriterSettings { OmitXmlDeclaration = true }))
                                    new DataContractSerializer(typeof(OrganizationResponse), "ExecuteResult", services.NamespaceName)
                                        .WriteObject(writer, new OrganizationResponse { ResponseName = name });
                                response = $"<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\"><s:Body><ExecuteResponse xmlns=\"{services}\">{result}</ExecuteResponse></s:Body></s:Envelope>";
                            }
                            byte[] payload = Encoding.UTF8.GetBytes(response);
                            byte[] prefix = Encoding.ASCII.GetBytes($"HTTP/1.1 {(throttle ? "500 Internal Server Error" : "200 OK")}\r\nContent-Type: text/xml; charset=utf-8\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                            await stream.WriteAsync(prefix, 0, prefix.Length, _timeout.Token);
                            await stream.WriteAsync(payload, 0, payload.Length, _timeout.Token);
                        }
                    }
                });
            }

            public void Dispose()
            {
                bool wasPending = !Completion.IsCompleted;
                _listener.Stop();
                _timeout.Cancel();
                try
                {
                    Completion.GetAwaiter().GetResult();
                }
                catch (SocketException) when (wasPending && _timeout.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (wasPending && _timeout.IsCancellationRequested) { }
                finally
                {
                    _stopOnTimeout.Dispose();
                    _timeout.Dispose();
                }
            }
        }
    }
}
