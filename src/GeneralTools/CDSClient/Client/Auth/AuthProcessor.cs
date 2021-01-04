using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.PowerPlatform.Cds.Client.Utils;
using Microsoft.Xrm.Sdk.WebServiceClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Description;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Auth
{
    internal class AuthProcessor
    {
		/// <summary>
		/// Executes Authentication against a service
		/// </summary>
		/// <param name="serviceUrl"></param>
		/// <param name="clientCredentials"></param>
		/// <param name="user"></param>
		/// <param name="clientId"></param>
		/// <param name="redirectUri"></param>
		/// <param name="promptBehavior"></param>
		/// <param name="isOnPrem"></param>
		/// <param name="authority"></param>
		/// <param name="userCert">Certificate of provided to login with</param>
		/// <param name="logSink">(optional) Initialized CdsTraceLogger Object</param>
		/// <param name="useDefaultCreds">(optional) if set, tries to login as the current user.</param>
		/// <param name="msalAuthClient">Object of either confidential or public client</param>
		/// <param name="clientSecret"></param>
		/// <param name="addVersionInfoToUri">indicates if the serviceURI should be updated to include the /web?sdk version</param>
		/// <returns>AuthenticationResult containing a JWT Token for the requested Resource and user/app</returns>
		internal async static Task<ExecuteAuthenticationResults> ExecuteAuthenticateServiceProcessAsync(
			Uri serviceUrl,
			ClientCredentials clientCredentials,
			X509Certificate2 userCert,
			string clientId,
			Uri redirectUri,
			PromptBehavior promptBehavior,
			bool isOnPrem,
			string authority,
			object msalAuthClient ,
			CdsTraceLogger logSink = null,
			bool useDefaultCreds = false,
			SecureString clientSecret = null,
			bool addVersionInfoToUri = true,
			IAccount user = null
			)
		{
			ExecuteAuthenticationResults processResult = new ExecuteAuthenticationResults();
			bool createdLogSource = false;

			AuthenticationResult authenticationResult = null;

			try
			{
				if (logSink == null)
				{
					// when set, the log source is locally created. 
					createdLogSource = true;
					logSink = new CdsTraceLogger();
				}

				string Authority = string.Empty;
				string Resource = string.Empty;

				bool clientCredentialsCheck = clientCredentials != null && clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName) && !string.IsNullOrEmpty(clientCredentials.UserName.Password);
				Resource = serviceUrl.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
				if (!Resource.EndsWith("/"))
					Resource += "/";

				if (addVersionInfoToUri)
					processResult.TargetServiceUrl = GetUriBuilderWithVersion(serviceUrl).Uri;
				else
					processResult.TargetServiceUrl = serviceUrl;

				if (!string.IsNullOrWhiteSpace(authority))
				{
					//Overriding the tenant specific authority if clientCredentials are null
					Authority = authority;
				}
				else
				{
					var rslt = GetAuthorityFromTargetServiceAsync(ClientServiceProviders.Instance.GetService<IHttpClientFactory>(), processResult.TargetServiceUrl, logSink).ConfigureAwait(false).GetAwaiter().GetResult();
					if (!string.IsNullOrEmpty(rslt.Authority))
                    {
						Authority = rslt.Authority;
						Resource = rslt.Resource;
                    }
					else
						throw new ArgumentNullException("Authority", "Need a non-empty authority");
				}
				//	clientCredentialsCheck = false;  // Forcing system to provide a UX popup vs UID/PW

				// Assign outbound properties. 
				processResult.Resource = Resource;
				processResult.Authority = Authority;

				logSink.Log("AuthenticateService - found authority with name " + (string.IsNullOrEmpty(Authority) ? "<Not Provided>" : Authority));
				logSink.Log("AuthenticateService - found resource with name " + (string.IsNullOrEmpty(Resource) ? "<Not Provided>" : Resource));

				Uri ResourceUri = new Uri(Resource);
                // Add Scope, 
                List<string> Scopes = Utilities.AddScope($"{Resource}/user_impersonation");

				AuthenticationResult _authenticationResult = null;
				if (userCert != null || clientSecret != null)
				{
					// Add Scope, 
					Scopes.Clear();
					Scopes = Utilities.AddScope($"{Resource}.default" , Scopes);

					IConfidentialClientApplication cApp = null;
					ConfidentialClientApplicationBuilder cAppBuilder = null;

					if (msalAuthClient is IConfidentialClientApplication)
					{
						cApp = (IConfidentialClientApplication)msalAuthClient;
					}
					else
					{
						cAppBuilder = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(
						new ConfidentialClientApplicationOptions()
						{
							ClientId = clientId,
							EnablePiiLogging = true,
							LogLevel = LogLevel.Verbose,
						})
						.WithAuthority(Authority)
						.WithLogging(Microsoft.PowerPlatform.Cds.Client.Utils.ADALLoggerCallBack.Log);
					}

					if (userCert != null)
					{
						logSink.Log("Initial ObtainAccessToken - CERT", TraceEventType.Verbose);
						cApp = cAppBuilder.WithCertificate(userCert).Build();
						_authenticationResult = await ObtainAccessTokenAsync(cApp, Scopes, logSink);
					}
					else
					{
						if (clientSecret != null)
						{
							logSink.Log("Initial ObtainAccessToken - Client Secret", TraceEventType.Verbose);
							cApp = cAppBuilder.WithClientSecret(clientSecret.ToUnsecureString()).Build();
							_authenticationResult = await ObtainAccessTokenAsync(cApp, Scopes, logSink);
						}
						else
							throw new Exception("Invalid Cert or Client Secret Auth flow");
					}

					// Update the MSAL Client handed back.
					processResult.MsalAuthClient = cApp;
				}
				else
				{
					PublicClientApplicationBuilder cApp = null;
					IPublicClientApplication pApp = null;
					if (msalAuthClient is IPublicClientApplication)
					{
						pApp = (IPublicClientApplication)msalAuthClient;
					}
					else
					{
						cApp = PublicClientApplicationBuilder.CreateWithApplicationOptions(
							new PublicClientApplicationOptions()
							{
								ClientId = clientId,
								EnablePiiLogging = true,
								RedirectUri = redirectUri.ToString(),
								LogLevel = LogLevel.Verbose,
							})
						.WithAuthority(Authority)
						.WithLogging(Microsoft.PowerPlatform.Cds.Client.Utils.ADALLoggerCallBack.Log);

						pApp = cApp.Build();
					}

					//Run user Auth flow. 
					_authenticationResult = await ObtainAccessTokenAsync(pApp, Scopes, user, promptBehavior, clientCredentials, useDefaultCreds, logSink);

					// Assign the application back out
					processResult.MsalAuthClient = pApp;

					//Assigning the authority to ref object to pass back to ConnMgr to store the latest Authority in Credential Manager.
					authority = Authority;
				}

				if (_authenticationResult != null && _authenticationResult.Account != null)
				{
					//To use same userId while connecting to OrgService (ConnectAndInitCrmOrgService)
					//_userId = _authenticationResult.Account;
					processResult.UserIdent = _authenticationResult.Account;
				}

				if (null == _authenticationResult)
				{
					throw new ArgumentNullException("AuthenticationResult");
				}
				authenticationResult = _authenticationResult;
				processResult.MsalAuthResult = authenticationResult;
			}
			catch (AggregateException ex)
			{
				if (ex.InnerException is Microsoft.Identity.Client.MsalException)
				{
					var errorHandledResult = await ProcessAdalExecptionAsync(serviceUrl, clientCredentials, userCert, clientId, redirectUri, promptBehavior, isOnPrem, authority , msalAuthClient, logSink, useDefaultCreds , (Microsoft.Identity.Client.MsalException)ex.InnerException);
					if (errorHandledResult != null)
						processResult = errorHandledResult;
				}
				else
				{
					logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - General ADAL Error", TraceEventType.Error, ex);
					logSink.Log(ex);
					throw;
				}
			}
			catch (Microsoft.Identity.Client.MsalException ex)
			{
				var errorHandledResult = await ProcessAdalExecptionAsync(serviceUrl, clientCredentials, userCert, clientId, redirectUri, promptBehavior, isOnPrem, authority, msalAuthClient, logSink, useDefaultCreds,  ex);
				if (errorHandledResult != null)
					processResult = errorHandledResult;
			}
			catch (System.Exception ex)
			{
				logSink.Log("ERROR REQUESTING Token FROM THE Authentication context", TraceEventType.Error);
				logSink.Log(ex);
				throw;
			}
			finally
			{
				if (createdLogSource) // Only dispose it if it was created locally. 
					logSink.Dispose();
			}
			return processResult;
		}



		/// <summary>
		///  Token refresh flow for MSAL User Flows.
		/// </summary>
		/// <param name="publicAppClient">MSAL Client to use.</param>
		/// <param name="scopes">Scopes to send in.</param>
		/// <param name="account"></param>
		/// <param name="promptBehavior">prompting behavior</param>
		/// <param name="clientCredentials">user credential package</param>
		/// <param name="useDefaultCreds">should system default creds be used</param>
		/// <param name="logSink">logger to write logs too.</param>
		/// <returns></returns>
		internal async static Task<AuthenticationResult> ObtainAccessTokenAsync(
			IPublicClientApplication publicAppClient,
			List<string> scopes,
			IAccount account,
			PromptBehavior promptBehavior,
			ClientCredentials clientCredentials,
			bool useDefaultCreds = false,
			CdsTraceLogger logSink = null)
		{
			// This works for user Auth flows. 
			AuthenticationResult _authenticationResult = null;
			bool clientCredentialsCheck = clientCredentials != null && clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName) && !string.IsNullOrEmpty(clientCredentials.UserName.Password);
			// Login user hint 
			string loginUserHint = (clientCredentials != null && clientCredentials.UserName != null) ? clientCredentials.UserName.UserName : string.Empty;
			if (publicAppClient != null)
			{
				if (clientCredentialsCheck && !useDefaultCreds && !(promptBehavior == PromptBehavior.Always || promptBehavior == PromptBehavior.SelectAccount))
				{
					logSink.Log("ObtainAccessToken - CRED", TraceEventType.Verbose);
					_authenticationResult = publicAppClient.AcquireTokenByUsernamePassword(scopes, clientCredentials.UserName.UserName, CdsServiceClient.MakeSecureString(clientCredentials.UserName.Password)).ExecuteAsync().Result;
				}
				else
				{
					if (useDefaultCreds)
					{
						if (!string.IsNullOrEmpty(loginUserHint))
						{
							logSink.Log("ObtainAccessToken - DEFAULT CREDS - Passed UPN", TraceEventType.Verbose);
							_authenticationResult = await publicAppClient.AcquireTokenByIntegratedWindowsAuth(scopes).WithUsername(loginUserHint).ExecuteAsync();
						}
						else
						{
							logSink.Log("ObtainAccessToken - DEFAULT CREDS", TraceEventType.Verbose);
							_authenticationResult = await publicAppClient.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync();
						}
					}
					else
					{
						logSink.Log(string.Format("ObtainAccessToken - PROMPT - Behavior: {0}", promptBehavior), TraceEventType.Verbose);
						Microsoft.Identity.Client.Prompt? userPrompt = null;
						switch (promptBehavior)
						{
							case PromptBehavior.Auto:
								break;
							case PromptBehavior.Always:
								userPrompt = Microsoft.Identity.Client.Prompt.ForceLogin;
								break;
							case PromptBehavior.Never:
							case PromptBehavior.RefreshSession:
								userPrompt = Microsoft.Identity.Client.Prompt.NoPrompt;
								break;
							case PromptBehavior.SelectAccount:
								userPrompt = Microsoft.Identity.Client.Prompt.SelectAccount;
								break;
							default:
								break;
						}

						if (userPrompt != null)
						{
							_authenticationResult = await publicAppClient.AcquireTokenInteractive(scopes).WithLoginHint(loginUserHint).WithPrompt(userPrompt.Value).ExecuteAsync();
						}
						else
						{
							if (account != null)
							{
								_authenticationResult = await publicAppClient.AcquireTokenSilent(scopes, account).ExecuteAsync();
							}
							else
							{
								_authenticationResult = await publicAppClient.AcquireTokenInteractive(scopes).WithLoginHint(loginUserHint).ExecuteAsync();
							}
						}
					}
				}
			}
			else
			{
				// throw here. 
			}
			return _authenticationResult;
		}


		/// <summary>
		/// Acquires Confidential client token.
		/// </summary>
		/// <param name="confidentialAppClient">Confidential client application</param>
		/// <param name="scopes">Scope List</param>
		/// <param name="logSink">Logger to use</param>
		/// <returns>Authentication Result with updated token</returns>
		internal async static Task<AuthenticationResult> ObtainAccessTokenAsync(
			IConfidentialClientApplication confidentialAppClient,
			List<string> scopes,
			CdsTraceLogger logSink = null)
		{
			// This works for user Auth flows. 
			AuthenticationResult _authenticationResult = null;
			if (confidentialAppClient != null)
			{
				logSink.Log("ObtainAccessToken - Confidential Client", TraceEventType.Verbose);
				_authenticationResult = await confidentialAppClient.AcquireTokenForClient(scopes).ExecuteAsync();
			}
			else
			{
				// throw here. 
			}
			return _authenticationResult;
		}


		/// <summary>
		/// Forming version tagged UriBuilder
		/// </summary>
		/// <param name="discoveryServiceUri"></param>
		/// <returns></returns>
		internal static UriBuilder GetUriBuilderWithVersion(Uri discoveryServiceUri)
		{
			UriBuilder webUrlBuilder = new UriBuilder(discoveryServiceUri);
			string webPath = "web";

			if (!discoveryServiceUri.AbsolutePath.EndsWith(webPath))
			{
				if (discoveryServiceUri.AbsolutePath.EndsWith("/"))
					webUrlBuilder.Path = string.Concat(webUrlBuilder.Path, webPath);
				else
					webUrlBuilder.Path = string.Concat(webUrlBuilder.Path, "/", webPath);
			}

			UriBuilder versionTaggedUriBuilder = new UriBuilder(webUrlBuilder.Uri);
			string version = FileVersionInfo.GetVersionInfo(typeof(OrganizationWebProxyClient).Assembly.Location).FileVersion;
			string versionQueryStringParameter = string.Format("SDKClientVersion={0}", version);

			if (string.IsNullOrEmpty(versionTaggedUriBuilder.Query))
			{
				versionTaggedUriBuilder.Query = versionQueryStringParameter;
			}
			else if (!versionTaggedUriBuilder.Query.Contains("SDKClientVersion="))
			{
				versionTaggedUriBuilder.Query = string.Format("{0}&{1}", versionTaggedUriBuilder.Query, versionQueryStringParameter);
			}

			return versionTaggedUriBuilder;
		}


		private const string AuthenticateHeader = "WWW-Authenticate";
		private const string Bearer = "bearer";
		private const string AuthorityKey = "authorization_uri";
		private const string ResourceKey = "resource_id";

		internal class AuthRoutingProperties
        {
            public string Authority { get; set; }
            public string Resource { get; set; }
        }

		/// <summary>
		/// Get authority and resource for this instance.
		/// </summary>
		/// <param name="targetServiceUrl">URI to query</param>
		/// <param name="logger">Logger to write info too</param>
		/// <param name="clientFactory">HTTP Client factory to use for this request.</param>
		/// <returns></returns>
		private static async Task<AuthRoutingProperties> GetAuthorityFromTargetServiceAsync(IHttpClientFactory clientFactory, Uri targetServiceUrl , CdsTraceLogger logger )
        {
			AuthRoutingProperties authRoutingProperties = new AuthRoutingProperties();
			var client = clientFactory.CreateClient("CdsHttpClientFactory");
			var rslt = await client.GetAsync(targetServiceUrl).ConfigureAwait(false);

			if ( rslt.StatusCode == System.Net.HttpStatusCode.NotFound || rslt.StatusCode == System.Net.HttpStatusCode.BadRequest )
			{
				// didnt find endpoint. 
				logger.Log($"Failed to get Authority and Resource error. Attempt to Access Endpoint {targetServiceUrl.ToString()} resulted in {rslt.StatusCode}.", TraceEventType.Error);
				return authRoutingProperties;
			}

			if (rslt.Headers.Contains("WWW-Authenticate"))
            {
				var authenticateHeader = rslt.Headers.GetValues("WWW-Authenticate").FirstOrDefault();
				authenticateHeader = authenticateHeader.Trim();

				// This also checks for cases like "BearerXXXX authorization_uri=...." and "Bearer" and "Bearer "
				if (!authenticateHeader.StartsWith(Bearer, StringComparison.OrdinalIgnoreCase)
					|| authenticateHeader.Length < Bearer.Length + 2
					|| !char.IsWhiteSpace(authenticateHeader[Bearer.Length]))
				{
					//var ex = new ArgumentException(AdalErrorMessage.InvalidAuthenticateHeaderFormat,
					//	nameof(authenticateHeader));
					//CoreLoggerBase.Default.Error(AdalErrorMessage.InvalidAuthenticateHeaderFormat);
					//CoreLoggerBase.Default.ErrorPii(ex);
					//throw ex;
				}

				authenticateHeader = authenticateHeader.Substring(Bearer.Length).Trim();

				IDictionary<string, string> authenticateHeaderItems = null;
				try
				{
					authenticateHeaderItems =
						EncodingHelper.ParseKeyValueListStrict(authenticateHeader, ',', false, true);
				}
				catch //(ArgumentException ex)
				{
					//var newEx = new ArgumentException(AdalErrorMessage.InvalidAuthenticateHeaderFormat,
					//	nameof(authenticateHeader), ex);
					//CoreLoggerBase.Default.Error(AdalErrorMessage.InvalidAuthenticateHeaderFormat);
					//CoreLoggerBase.Default.ErrorPii(newEx);
					//throw newEx;
				}

				if (authenticateHeaderItems != null)
				{
					string param;
					authenticateHeaderItems.TryGetValue(AuthorityKey, out param);
					authRoutingProperties.Authority =
						param.Replace("oauth2/authorize", "") // swap out the old oAuth pattern.
						.Replace("common" , "organizations"); // swap common for organizations because MSAL reasons. 
					authenticateHeaderItems.TryGetValue(ResourceKey, out param);
					authRoutingProperties.Resource = param;
				}
			}

			return authRoutingProperties;
		}

		/// <summary>
		/// Process ADAL exception and provide common handlers.
		/// </summary>
		/// <param name="serviceUrl"></param>
		/// <param name="clientCredentials"></param>
		/// <param name="userCert"></param>
		/// <param name="clientId"></param>
		/// <param name="redirectUri"></param>
		/// <param name="promptBehavior"></param>
		/// <param name="isOnPrem"></param>
		/// <param name="authority"></param>
		/// <param name="logSink"></param>
		/// <param name="useDefaultCreds"></param>
		/// <param name="adalEx"></param>
		/// <param name="msalAuthClient"></param>
		private async static Task<ExecuteAuthenticationResults> ProcessAdalExecptionAsync(Uri serviceUrl, ClientCredentials clientCredentials, X509Certificate2 userCert, string clientId, Uri redirectUri, PromptBehavior promptBehavior, bool isOnPrem, string authority , object msalAuthClient, CdsTraceLogger logSink, bool useDefaultCreds, Microsoft.Identity.Client.MsalException adalEx)
		{
			if (adalEx.ErrorCode.Equals("interaction_required", StringComparison.OrdinalIgnoreCase) ||
				adalEx.ErrorCode.Equals("user_password_expired", StringComparison.OrdinalIgnoreCase) ||
				adalEx.ErrorCode.Equals("password_required_for_managed_user", StringComparison.OrdinalIgnoreCase) ||
				adalEx is Microsoft.Identity.Client.MsalUiRequiredException)
			{
				logSink.Log("ERROR REQUESTING TOKEN FROM THE AUTHENTICATION CONTEXT - USER intervention required", TraceEventType.Warning);
				// ADAL wants the User to do something,, determine if we are able to see a user
				if (promptBehavior == PromptBehavior.Always || promptBehavior == PromptBehavior.Auto)
				{
					// Switch to MFA user mode..
					Microsoft.Identity.Client.IAccount user = null;  //TODO:UPDATE THIS OR REMOVE AS WE DETERMIN HOW TO SOLVE THIS ISSUE IN MSAL //  new Microsoft.Identity.Client.AccountId(); 
					user = null;
					//user = new UserIdentifier(clientCredentials.UserName.UserName, UserIdentifierType.OptionalDisplayableId);
					return await ExecuteAuthenticateServiceProcessAsync(serviceUrl, null, userCert, clientId, redirectUri, promptBehavior, isOnPrem, authority, msalAuthClient, logSink, useDefaultCreds: useDefaultCreds, user: user);
				}
				else
				{
					logSink.Log("ERROR REQUESTING TOKEN FROM THE AUTHENTICATION CONTEXT - USER intervention required but not permitted by prompt behavior", TraceEventType.Error, adalEx);
					throw adalEx;
				}
			}
			else
			{
				logSink.Log("ERROR REQUESTING Token FROM THE Authentication context - General ADAL Error", TraceEventType.Error, adalEx);
				throw adalEx;
			}
		}
	}
}
