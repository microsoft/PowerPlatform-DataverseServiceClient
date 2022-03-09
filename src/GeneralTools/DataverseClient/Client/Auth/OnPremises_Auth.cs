using Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.ServiceModel.Description;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth
{
    /// <summary>
    /// Authentication for Non-OAuth Onprem. 
    /// </summary>
    internal static class OnPremises_Auth
    {
        /// <summary>
        /// Creates and authenticates the Service Proxy for the organization service for OnPremises Dataverse
        /// </summary>
        /// <typeparam name="T">Service Management Type</typeparam>
        /// <param name="servicecfg">Initialized Service Management object or null. </param>
        /// <param name="ServiceUri">URL to connect too</param>
        /// <param name="homeRealm">HomeRealm URI</param>
        /// <param name="userCredentials">User Credentials object</param>
        /// <param name="LogString">Log Preface string. </param>
        /// <param name="MaxConnectionTimeout">Max Connection timeout setting. </param>
        /// <param name="logSink">(optional) Initialized DataverseTraceLogger Object</param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", MessageId = "OutObject")]
        internal static object CreateAndAuthenticateProxy<T>(IServiceManagement<T> servicecfg,
            Uri ServiceUri,
            Uri homeRealm,
            ClientCredentials userCredentials,
            string LogString,
            TimeSpan MaxConnectionTimeout,
            DataverseTraceLogger logSink = null)
        {
            bool createdLogSource = false;
            Stopwatch dtProxyCreate = new Stopwatch();
            dtProxyCreate.Start();
            Stopwatch dtConnectTimeCheck = new Stopwatch();
            try
            {
                if (logSink == null)
                {
                    // when set, the log source is locally created.
                    createdLogSource = true;
                    logSink = new DataverseTraceLogger();
                }


                object OutObject = null;
                if (servicecfg == null)
                {
                    logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - attempting to connect to On-Premises Dataverse server @ {1}", LogString, ServiceUri.ToString()), TraceEventType.Verbose);

                    // Create the Service configuration for that URL
                    dtConnectTimeCheck.Restart();
                    servicecfg = ServiceConfigurationFactoryAsync.CreateManagement<T>(ServiceUri);
                    dtConnectTimeCheck.Stop();
                    if (servicecfg == null)
                        return null;
                    logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - created Dataverse server proxy configuration for {1} - duration: {2}", LogString, ServiceUri.ToString(), dtConnectTimeCheck.Elapsed.ToString()), TraceEventType.Verbose);
                }
                else
                {
                    logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - will use user provided {1} to connect to Dataverse ", LogString, typeof(T).ToString()), TraceEventType.Verbose);
                }

                // Auth
                logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - proxy requiring authentication type : {1} ", LogString, servicecfg.AuthenticationType), TraceEventType.Verbose);
                // Determine the type of authentication required.
                if (servicecfg.AuthenticationType != AuthenticationProviderType.ActiveDirectory)
                {
                    // Connect via anything other then AD.
                    // Setup for Auth Check Performance.
                    dtConnectTimeCheck.Restart();

                    // Deal with IFD QurikyNess in ADFS configuration,  where ADFS can be configured to fall though to Kerb Auth.
                    AuthenticationCredentials authCred = ClaimsIFDFailOverAuth<T>(servicecfg, homeRealm, userCredentials);
                    dtConnectTimeCheck.Stop();

                    // If is Federation and HomeRealm is not null, and HomeRealm is Not the same as the SecureTokeServiceIdentifier
                    // Run Secondary auth to auth the Token to the right source.
                    SecurityTokenResponse AuthKey = null;
                    if (servicecfg.AuthenticationType == AuthenticationProviderType.Federation &&
                            homeRealm != null &&
                            !string.IsNullOrWhiteSpace(homeRealm.ToString()) &&
                            (homeRealm.ToString() != servicecfg.PolicyConfiguration.SecureTokenServiceIdentifier))
                    {
                        logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - Initial Authenticated via {1} {3} . Auth Elapsed:{2}", LogString, servicecfg.AuthenticationType, dtConnectTimeCheck.Elapsed.ToString(), homeRealm.ToString()), TraceEventType.Verbose);
                        logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - Relaying Auth to Resource Server: From {1} to {2}", LogString, homeRealm.ToString(), servicecfg.PolicyConfiguration.SecureTokenServiceIdentifier), TraceEventType.Verbose);
                        dtConnectTimeCheck.Restart();
                        // Auth token against the correct server.
                        AuthenticationCredentials authCred2 = servicecfg.Authenticate(new AuthenticationCredentials()
                        {
                            SecurityTokenResponse = authCred.SecurityTokenResponse
                        });
                        dtConnectTimeCheck.Stop();

                        if (authCred2 != null)
                        {
                            AuthKey = authCred2.SecurityTokenResponse;
                            logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - Authenticated via {1}. Auth Elapsed:{2}", LogString, servicecfg.AuthenticationType, dtConnectTimeCheck.Elapsed.ToString()), TraceEventType.Verbose);
                        }
                        else
                            logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - FAILED Authentication via {1}. Auth Elapsed:{2}", LogString, servicecfg.AuthenticationType, dtConnectTimeCheck.Elapsed.ToString()), TraceEventType.Verbose);

                    }
                    else
                    {
                        if (authCred != null)
                        {
                            AuthKey = authCred.SecurityTokenResponse;
                            logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - Authenticated via {1}. Auth Elapsed:{2}", LogString, servicecfg.AuthenticationType, dtConnectTimeCheck.Elapsed.ToString()), TraceEventType.Verbose);
                        }
                        else
                            logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - Failed Authentication via {1}. Auth Elapsed:{2}", LogString, servicecfg.AuthenticationType, dtConnectTimeCheck.Elapsed.ToString()), TraceEventType.Verbose);

                    }
                    //if (typeof(T) == typeof(IDiscoveryService))
                    //	OutObject = new DiscoveryServiceProxy((IServiceManagement<IDiscoveryService>)servicecfg, AuthKey);

                    if (typeof(T) == typeof(IOrganizationServiceAsync))
                        OutObject = new ManagedTokenOrganizationServiceProxy((IServiceManagement<IOrganizationServiceAsync>)servicecfg, AuthKey, userCredentials);
                }
                else
                {
                    if (typeof(T) == typeof(IOrganizationServiceAsync))
                        OutObject = new OrganizationServiceProxyAsync((IServiceManagement<IOrganizationServiceAsync>)servicecfg, userCredentials);
                }

                logSink.Log(string.Format(CultureInfo.InvariantCulture, "{0} - service proxy created - total create duration: {1}", LogString, dtProxyCreate.Elapsed.ToString()), TraceEventType.Verbose);

                //Update the Timeout in case the MaxCrmConnectionTimeOutMinutes is Set in Config File.
                if (OutObject != null)
                {
                    if (OutObject is OrganizationServiceProxyAsync)
                        ((OrganizationServiceProxyAsync)OutObject).Timeout = MaxConnectionTimeout;
                }

                return OutObject;
            }
            catch
            {
                throw;
            }
            finally
            {

                if (createdLogSource) // Only dispose it if it was created locally.
                    logSink.Dispose();
            }
        }

        /// <summary>
        /// Handles direct authentication and fall though support to Kerb for Federation environments where configured for fall though
        /// </summary>
        /// <typeparam name="T">Type of Service being authenticated</typeparam>
        /// <param name="servicecfg">Service configuration</param>
        /// <param name="homeRealm">HomeRelam of the service</param>
        /// <param name="userCredentials">User Credentials</param>
        /// <param name="tryNetworkCred">Internal Fall though switch</param>
        /// <param name="depthLevel">internal call back value</param>
        /// <returns>AuthenticationCredentials configured or null. </returns>
        private static AuthenticationCredentials ClaimsIFDFailOverAuth<T>(IServiceManagement<T> servicecfg, Uri homeRealm, ClientCredentials userCredentials, int depthLevel = 0, bool tryNetworkCred = false)
        {
            AuthenticationCredentials authCred = new AuthenticationCredentials();

            // Head off a runaway if one occurs.
            if (depthLevel > 10)
                return null;

            // If Im starting with a NetworkCred,  try to turn that into a Client Cred User ID for federation.
            if (tryNetworkCred == false &&
                servicecfg.AuthenticationType == AuthenticationProviderType.Federation &&
                userCredentials.Windows != null &&
                userCredentials.Windows.ClientCredential != null &&
                !string.IsNullOrWhiteSpace(userCredentials.Windows.ClientCredential.UserName))
            {
                // Restructure user Account Creds for Federation
                ClientCredentials userCredentials2 = new ClientCredentials();

                // Updating the restructure process to remove corrective logic as, based on the configuration of the
                userCredentials2.UserName.UserName = userCredentials.Windows.ClientCredential.UserName;
                userCredentials2.UserName.Password = userCredentials.Windows.ClientCredential.Password;
                authCred.ClientCredentials = userCredentials2;
            }
            else
                authCred.ClientCredentials = userCredentials;

            // Claims
            if (homeRealm != null)
                authCred.HomeRealm = homeRealm;

            // Deal with an incorrect configuration here.
            // Home Realm should not be used if the Service Identifier and the homeRealm are the same thing.
            if (homeRealm != null && homeRealm.ToString() != servicecfg.PolicyConfiguration.SecureTokenServiceIdentifier)
                authCred.AppliesTo = new Uri(servicecfg.PolicyConfiguration.SecureTokenServiceIdentifier);

            // Run Authentication
            // Failure will generate Exceptions.
            authCred = servicecfg.Authenticate(authCred);
            if (authCred != null && authCred.SecurityTokenResponse == null && userCredentials.Windows != null && userCredentials.Windows.ClientCredential != null)
            {
                // This code exists to deal with Federation configurations that "fall though" to claims from ADFS.. more commonly known as IFD.
                return ClaimsIFDFailOverAuth<T>(servicecfg, homeRealm, userCredentials, ++depthLevel, true);
            }
            else
                return authCred;
        }


    }
}
