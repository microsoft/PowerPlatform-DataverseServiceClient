using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Xml;

namespace Microsoft.PowerPlatform.Cds.Client
{
    /// <summary>
    /// Adding support to Send the User Agent Header.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
    internal class CdsServiceTelemetryBehaviors : IEndpointBehavior, IClientMessageInspector
    {
        #region Vars
        private CdsConnectionService _callerCdsConnectionServiceHandler;
        private int _maxFaultSize = -1;
        private int _maxReceivedMessageSize = -1;
        private string _userAgent;
        #endregion

        #region Const
        private const int MAXFAULTSIZEDEFAULT = 131072;
        private const int MAXRECVMESSAGESIZEDEFAULT = 2147483647;
        #endregion

        /// <summary>
        /// Constructor for building the hook to call into the platform.
        /// </summary>
        public CdsServiceTelemetryBehaviors(CdsConnectionService cli)
        {
            _callerCdsConnectionServiceHandler = cli;

            // reading overrides from app config if present..
            // these values override the values that are set on the client from the server. 
            CdsTraceLogger logg = new CdsTraceLogger();
            try
            {
                // Initialize user agent
                _userAgent = "Unknown";
                if (AppDomain.CurrentDomain != null)
                {
                    _userAgent = AppDomain.CurrentDomain.FriendlyName;
                }

                _userAgent = $"{_userAgent} (CdsSvcClient:{Environs.FileVersion})";

                if (_maxFaultSize == -1 && ConfigurationManager.AppSettings.AllKeys.Contains("MaxFaultSizeOverride"))
                {
                    var maxFaultSz = ConfigurationManager.AppSettings["MaxFaultSizeOverride"];
                    if (maxFaultSz is string && !string.IsNullOrWhiteSpace(maxFaultSz))
                    {
                        int.TryParse(maxFaultSz, out _maxFaultSize);
                        if (_maxFaultSize != -1)
                        {
                            if (_maxFaultSize < MAXFAULTSIZEDEFAULT)
                            {
                                _maxFaultSize = -1;
                                logg.Log($"Failed to set MaxFaultSizeOverride property. Value found: {maxFaultSz}. Size must be larger then {MAXFAULTSIZEDEFAULT}.", System.Diagnostics.TraceEventType.Warning);
                            }
                        }
                    }
                    else
                        logg.Log($"Failed to parse MaxFaultSizeOverride property. Value found: {maxFaultSz}. MaxFaultSizeOverride must be a valid integer.", System.Diagnostics.TraceEventType.Warning);
                }

                if (_maxReceivedMessageSize == -1 && ConfigurationManager.AppSettings.AllKeys.Contains("MaxReceivedMessageSizeOverride"))
                {
                    var maxRecvSz = ConfigurationManager.AppSettings["MaxReceivedMessageSizeOverride"];
                    if (maxRecvSz is string && !string.IsNullOrWhiteSpace(maxRecvSz))
                    {
                        int.TryParse(maxRecvSz, out _maxReceivedMessageSize);
                        if (_maxReceivedMessageSize != -1)
                        {
                            if (_maxReceivedMessageSize < MAXRECVMESSAGESIZEDEFAULT)
                            {
                                _maxReceivedMessageSize = -1;
                                logg.Log($"Failed to set MaxReceivedMessageSizeOverride property. Value found: {maxRecvSz}. Size must be larger then {MAXRECVMESSAGESIZEDEFAULT}.", System.Diagnostics.TraceEventType.Warning);
                            }
                        }
                    }
                    else
                        logg.Log($"Failed to parse MaxReceivedMessageSizeOverride property. Value found: {maxRecvSz}. MaxReceivedMessageSizeOverride must be a valid integer.", System.Diagnostics.TraceEventType.Warning);
                }
            }
            catch (Exception ex)
            {
                logg.Log("Failed to process binding override properties,  Only MaxFaultSizeOverride and MaxReceivedMessageSizeOverride are supported and must be integers.", System.Diagnostics.TraceEventType.Warning, ex);
            }
            finally
            {
                logg.Dispose();
            }
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "1")]
        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
#if NET462
            clientRuntime.MessageInspectors.Add(this);
#else
            clientRuntime.ClientMessageInspectors.Add(this);
#endif

            // when Set, this will ask WCF to transit cookies. 
            if (_callerCdsConnectionServiceHandler.EnableCookieRelay)
            {
                if (endpoint.Binding is BasicHttpBinding)
                {
                    ((BasicHttpBinding)endpoint.Binding).AllowCookies = true;
                }
            }
            // Override the Max Fault size if required. 
            if (_maxFaultSize != -1)
            {
                clientRuntime.MaxFaultSize = _maxFaultSize;
            }
            else
            {
                // set default MaxFaultSize to 4 MB if not overridden to handle large exceptions
                // e.g. missing dependencies on solution import is very chatty and commonly exceeds 1 MB
                clientRuntime.MaxFaultSize = 4 * 1024 * 1024;
            }

            // Override the max received size if required. 
            if (_maxReceivedMessageSize != -1)
            {
                if (endpoint.Binding is BasicHttpBinding)
                {
                    ((BasicHttpBinding)endpoint.Binding).MaxReceivedMessageSize = _maxReceivedMessageSize;
                }
            }
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        /// <summary/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }

        /// <summary/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            // Try to get Request ID from inbound request. 
            Guid OrganizationRequestId = Guid.Empty;
            try
            {
                // Get a copy to work with. 
                using (MessageBuffer mbuff = request.CreateBufferedCopy(Int32.MaxValue))
                {
                    request = mbuff.CreateMessage();  // Copy it back to the request buffer. 
                    using (XmlDictionaryReader msgReader = mbuff.CreateMessage().GetReaderAtBodyContents())
                    {
                        msgReader.MoveToContent();
                        while (msgReader.Read())
                        {
                            if (msgReader.NodeType == XmlNodeType.Element && msgReader.LocalName.Equals("RequestId", StringComparison.OrdinalIgnoreCase))
                            {
                                string readValue = msgReader.ReadElementContentAsString();
                                if (!string.IsNullOrEmpty(readValue))
                                    Guid.TryParse(readValue, out OrganizationRequestId); // Find the request ID from the message and assign it to the new tracking id. 
                                break;
                            }
                        }
                    }
                    mbuff.Close();
                }
            }
            catch
            {
            }

            // Adding HTTP Headers
            object httpRequestMessageObject;
            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
            {
                HttpRequestMessageProperty httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                if (string.IsNullOrEmpty(httpRequestMessage.Headers[Utilities.CDSRequestHeaders.USER_AGENT_HTTP_HEADER]))
                {
                    httpRequestMessage.Headers[Utilities.CDSRequestHeaders.USER_AGENT_HTTP_HEADER] = _userAgent;
                }
                if (string.IsNullOrEmpty(httpRequestMessage.Headers[HttpRequestHeader.Connection]))
                {
                    httpRequestMessage.Headers.Add(HttpRequestHeader.Connection, Utilities.CDSRequestHeaders.CONNECTION_KEEP_ALIVE);
                }
                if (OrganizationRequestId != Guid.Empty && string.IsNullOrEmpty(httpRequestMessage.Headers[Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID]))
                {
                    httpRequestMessage.Headers[Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID] = OrganizationRequestId.ToString();
                }
                if ((_callerCdsConnectionServiceHandler != null && (_callerCdsConnectionServiceHandler.SessionTrackingId.HasValue && _callerCdsConnectionServiceHandler.SessionTrackingId.Value != Guid.Empty)) && string.IsNullOrEmpty(httpRequestMessage.Headers[Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID]))
                {
                    httpRequestMessage.Headers[Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID] = _callerCdsConnectionServiceHandler.SessionTrackingId.Value.ToString();
                }
                if ((_callerCdsConnectionServiceHandler != null && _callerCdsConnectionServiceHandler.ForceServerCacheConsistency && string.IsNullOrEmpty(httpRequestMessage.Headers[Utilities.CDSRequestHeaders.FORCE_CONSISTENCY])))
                {
                    httpRequestMessage.Headers[Utilities.CDSRequestHeaders.FORCE_CONSISTENCY] = "Strong";
                }
            }
            else
            {
                HttpRequestMessageProperty httpRequestMessage = new HttpRequestMessageProperty();
                httpRequestMessage.Headers.Add(Utilities.CDSRequestHeaders.USER_AGENT_HTTP_HEADER, _userAgent);
                httpRequestMessage.Headers.Add(HttpRequestHeader.Connection, Utilities.CDSRequestHeaders.CONNECTION_KEEP_ALIVE);

                if (OrganizationRequestId != Guid.Empty)
                {
                    httpRequestMessage.Headers.Add(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID, OrganizationRequestId.ToString());
                }
                if (_callerCdsConnectionServiceHandler != null && (_callerCdsConnectionServiceHandler.SessionTrackingId.HasValue && _callerCdsConnectionServiceHandler.SessionTrackingId.Value != Guid.Empty))
                {
                    httpRequestMessage.Headers.Add(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID, _callerCdsConnectionServiceHandler.SessionTrackingId.Value.ToString());
                }
                if ((_callerCdsConnectionServiceHandler != null && _callerCdsConnectionServiceHandler.ForceServerCacheConsistency && string.IsNullOrEmpty(httpRequestMessage.Headers[Utilities.CDSRequestHeaders.FORCE_CONSISTENCY])))
                {
                    httpRequestMessage.Headers.Add(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY, "Strong");
                }

                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
            }

            // Adding SOAP headers
            Guid callerId = Guid.Empty;
            if (_callerCdsConnectionServiceHandler != null)
            {
                if (_callerCdsConnectionServiceHandler.CdsWebClient != null)
                    callerId = _callerCdsConnectionServiceHandler.CdsWebClient.CallerId;
            }

            if (callerId == Guid.Empty) // Prefer the CRM Caller ID over hte AADObjectID. 
            {
                if (_callerCdsConnectionServiceHandler != null && (_callerCdsConnectionServiceHandler.CallerAADObjectId.HasValue && _callerCdsConnectionServiceHandler.CallerAADObjectId.Value != Guid.Empty))
                {
                    // Add Caller ID to the SOAP Envolope. 
                    // Set a header request with the AAD Caller Object ID. 
                    using (OperationContextScope scope = new OperationContextScope((IContextChannel)channel))
                    {
                        var AADCallerIdHeader = new MessageHeader<Guid>(_callerCdsConnectionServiceHandler.CallerAADObjectId.Value).GetUntypedHeader(Utilities.CDSRequestHeaders.AAD_CALLER_OBJECT_ID_HTTP_HEADER, "http://schemas.microsoft.com/xrm/2011/Contracts");
                        request.Headers.Add(AADCallerIdHeader);
                    }
                }
            }

            if (OrganizationRequestId != Guid.Empty)
            {
                using (OperationContextScope scope = new OperationContextScope((IContextChannel)channel))
                {
                    var ClientRequestHeader = new MessageHeader<Guid>(OrganizationRequestId).GetUntypedHeader(Utilities.CDSRequestHeaders.X_MS_CLIENT_REQUEST_ID, "http://schemas.microsoft.com/xrm/2011/Contracts");
                    request.Headers.Add(ClientRequestHeader);
                }
            }

            if (_callerCdsConnectionServiceHandler != null && (_callerCdsConnectionServiceHandler.SessionTrackingId.HasValue && _callerCdsConnectionServiceHandler.SessionTrackingId.Value != Guid.Empty))
            {
                using (OperationContextScope scope = new OperationContextScope((IContextChannel)channel))
                {
                    var ClientSessionRequestHeader = new MessageHeader<Guid>(_callerCdsConnectionServiceHandler.SessionTrackingId.Value).GetUntypedHeader(Utilities.CDSRequestHeaders.X_MS_CLIENT_SESSION_ID, "http://schemas.microsoft.com/xrm/2011/Contracts");
                    request.Headers.Add(ClientSessionRequestHeader);
                }
            }

            if ((_callerCdsConnectionServiceHandler != null && _callerCdsConnectionServiceHandler.ForceServerCacheConsistency))
            {
                using (OperationContextScope scope = new OperationContextScope((IContextChannel)channel))
                {
                    var ForceConsistencytHeader = new MessageHeader<string>("Strong").GetUntypedHeader(Utilities.CDSRequestHeaders.FORCE_CONSISTENCY, "http://schemas.microsoft.com/xrm/2011/Contracts");
                    request.Headers.Add(ForceConsistencytHeader);
                }
            }
            return null;
        }
    }
}
