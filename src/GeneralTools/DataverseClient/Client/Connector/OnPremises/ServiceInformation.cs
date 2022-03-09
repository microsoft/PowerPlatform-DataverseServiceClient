using System;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Protocols.WSTrust;
using System.IdentityModel.Tokens;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Common;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
	[SecurityPermission(SecurityAction.Demand, Unrestricted = true)]
	[SecuritySafeCritical]
	internal sealed partial class ServiceConfiguration<TService>
	{
		private ServiceEndpoint currentServiceEndpoint;

		public PolicyConfiguration PolicyConfiguration { get; set; }

		public ServiceEndpointMetadata ServiceEndpointMetadata { get; private set; }

		private ServiceConfiguration()
		{
		}

		/// <summary>
		/// Returns true if the AuthenticationType == Federation or LiveFederation
		/// </summary>
		private bool ClaimsEnabledService
		{
			get
			{
				return AuthenticationType == AuthenticationProviderType.Federation || AuthenticationType == AuthenticationProviderType.OnlineFederation;
			}
		}

		public ServiceConfiguration(Uri serviceUri)
			: this(serviceUri, false)
		{
		}

		internal ServiceConfiguration(Uri serviceUri, bool checkForSecondary)
		{
			ServiceUri = serviceUri;

			ServiceEndpointMetadata = ServiceMetadataUtility.RetrieveServiceEndpointMetadata(typeof(TService), ServiceUri, checkForSecondary);

			ClientExceptionHelper.ThrowIfNull(ServiceEndpointMetadata, "ServiceEndpointMetadata");

			if (ServiceEndpointMetadata.ServiceEndpoints.Count == 0)
			{
				StringBuilder errorBuilder = new StringBuilder();
				if (ServiceEndpointMetadata.MetadataConversionErrors.Count > 0)
				{
					foreach (MetadataConversionError error in ServiceEndpointMetadata.MetadataConversionErrors)
					{
						errorBuilder.Append(error.Message);
					}
				}

				throw new InvalidOperationException(ClientExceptionHelper.FormatMessage(0, "The provided uri did not return any Service Endpoints!\n{0}", errorBuilder.ToString()));
			}

			ServiceEndpoints = ServiceEndpointMetadata.ServiceEndpoints;

			if (CurrentServiceEndpoint != null)
			{
				CrossRealmIssuerEndpoints = new CrossRealmIssuerEndpointCollection();

				SetAuthenticationConfiguration();

				if (checkForSecondary)
				{
					SetEndpointSwitchingBehavior();
				}
				else
				{
					if (CurrentServiceEndpoint.Address.Uri != serviceUri)
					{
						ServiceMetadataUtility.ReplaceEndpointAddress(CurrentServiceEndpoint, serviceUri);
					}

					PrimaryEndpoint = serviceUri;
				}
			}
		}

		/// <summary>
		/// If there is no binding, there is nothing to do.  Otherwise, import the XRM Policy elements and set the issuers if claims.
		/// </summary>
		private void SetAuthenticationConfiguration()
		{
			if (CurrentServiceEndpoint.Binding == null)
			{
				return;
			}

			var bindingElements = CurrentServiceEndpoint.Binding.CreateBindingElements();

			var xrmPolicy = bindingElements.Find<AuthenticationPolicy>();
			if (xrmPolicy != null)
			{
				if (xrmPolicy.PolicyElements.ContainsKey("AuthenticationType"))
				{
					string type = xrmPolicy.PolicyElements["AuthenticationType"];
					if (!string.IsNullOrEmpty(type))
					{
						AuthenticationProviderType authType;
						if (Enum.TryParse<AuthenticationProviderType>(type, out authType))
						{
							switch (authType)
							{
								case AuthenticationProviderType.OnlineFederation:
									PolicyConfiguration = new OnlineFederationPolicyConfiguration(xrmPolicy);
									foreach (var onlineProvider in ((OnlineFederationPolicyConfiguration)PolicyConfiguration).OnlineProviders.Values)
									{
										IssuerEndpoints = ServiceMetadataUtility.RetrieveLiveIdIssuerEndpoints(onlineProvider);
									}

									break;
								case AuthenticationProviderType.Federation:
									// Set the issuer information
									IssuerEndpoints = ServiceMetadataUtility.RetrieveIssuerEndpoints(AuthenticationProviderType.Federation, ServiceEndpoints, true);
									PolicyConfiguration = new ClaimsPolicyConfiguration(xrmPolicy);
									break;
								default:
									PolicyConfiguration = new WindowsPolicyConfiguration(xrmPolicy);
									break;
							}

							return;
						}
					}
				}
			}
		}

		public Uri ServiceUri { get; internal set; }

		/// <summary>
		/// This defaults to the first avaialble endpoint in the ServiceEndpoints dictionary if it has not been set.
		/// </summary>
		public ServiceEndpoint CurrentServiceEndpoint
		{
			get
			{
				if (currentServiceEndpoint == null)
				{
					foreach (var endpoint in ServiceEndpoints.Values)
					{
						if (ServiceUri.Port == endpoint.Address.Uri.Port &&
						   ServiceUri.Scheme == endpoint.Address.Uri.Scheme)
						{
							currentServiceEndpoint = endpoint;
							break;
						}
					}
				}

				return currentServiceEndpoint;
			}

			set
			{
				currentServiceEndpoint = value;
			}
		}

		/// <summary>
		/// If there is a CurrentServiceEndpoint and the Service has been configured for claims (Federation,) then this
		/// is the endpoint used by the Secure Token Service (STS) to issue the trusted token.
		/// </summary>
		public IssuerEndpoint CurrentIssuer
		{
			get
			{
				if (CurrentServiceEndpoint != null)
				{
					return ServiceMetadataUtility.GetIssuer(CurrentServiceEndpoint.Binding);
				}

				return null;
			}

			set
			{
				if (CurrentServiceEndpoint != null)
				{
					CurrentServiceEndpoint.Binding = ServiceMetadataUtility.SetIssuer(CurrentServiceEndpoint.Binding, value);
				}
			}
		}

		/// <summary>
		/// Identifies whether the constructed service is using Claims (Federation) authentication or legacy AD/RPS.
		/// </summary>
		public AuthenticationProviderType AuthenticationType
		{
			get
			{
				if (PolicyConfiguration is WindowsPolicyConfiguration)
				{
					return AuthenticationProviderType.ActiveDirectory;
				}

				if (PolicyConfiguration is ClaimsPolicyConfiguration)
				{
					return AuthenticationProviderType.Federation;
				}

				if (PolicyConfiguration is LiveIdPolicyConfiguration)
				{
					return AuthenticationProviderType.LiveId;
				}

				if (PolicyConfiguration is OnlineFederationPolicyConfiguration)
				{
					return AuthenticationProviderType.OnlineFederation;
				}

				return AuthenticationProviderType.None;
			}
		}

		/// <summary>
		/// Contains the list of urls and binding information required in order to make a call to a WCF service.  If the service is configured
		/// for On-Premise use only, then the endpoint(s) contained within will NOT require the use of an Issuer Endpoint on the binding.
		/// 
		/// If the service is configured to use Claims (Federation,) then the binding on the service endpoint MUST be configured to use
		/// the appropriate Issuer Endpoint, i.e., UserNamePassword, Kerberos, etc.
		/// </summary>
		public ServiceEndpointDictionary ServiceEndpoints { get; internal set; }

		/// <summary>
		/// The following property contains the urls and binding information required to use a configured Secure Token Service (STS)
		/// for issuing a trusted token that the service endpoint will trust for authentication.
		/// 
		/// The available endpoints can vary, depending on how the administrator of the STS has configured the server, but may include 
		/// the following authentication methods:
		/// 
		/// 1.  UserName and Password
		/// 2.  Kerberos
		/// 3.  Certificate
		/// 4.  Asymmetric Token
		/// 5.  Symmetric Token
		/// </summary>
		public IssuerEndpointDictionary IssuerEndpoints { get; internal set; }

		/// <summary>
		/// Contains the STS IssuerEndpoints as determined dynamically by calls to AuthenticateCrossRealm.
		/// </summary>
		public CrossRealmIssuerEndpointCollection CrossRealmIssuerEndpoints { get; internal set; }

		private TokenServiceCredentialType _tokenEndpointType = TokenServiceCredentialType.AsymmetricToken;

		[SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", Justification = "Value is returned from method and cannot be disposed.")]
		public ChannelFactory<TService> CreateChannelFactory(TokenServiceCredentialType endpointType)
		{
			ClientExceptionHelper.ThrowIfNull(CurrentServiceEndpoint, "CurrentServiceEndpoint");

			if (ClaimsEnabledService)
			{
				IssuerEndpoint authEndpoint = null;

				authEndpoint = IssuerEndpoints.GetIssuerEndpoint(endpointType);
				if (authEndpoint != null)
				{
					lock (_lockObject)
					{
						CurrentServiceEndpoint.Binding = ServiceMetadataUtility.SetIssuer(CurrentServiceEndpoint.Binding, authEndpoint);
					}
				}
			}

			var factory = CreateLocalChannelFactory();
			factory.Credentials.SetSupportInteractive(false);

			return factory;
		}

		[SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", Justification = "Value is returned from method and cannot be disposed.")]
		public ChannelFactory<TService> CreateChannelFactory(ClientAuthenticationType clientAuthenticationType)
		{
			ClientExceptionHelper.ThrowIfNull(CurrentServiceEndpoint, "CurrentServiceEndpoint");

			if (ClaimsEnabledService)
			{
				IssuerEndpoint authEndpoint = null;

				TokenServiceCredentialType credentialType;
				if (clientAuthenticationType == ClientAuthenticationType.SecurityToken)
				{
					// Use symmetrictoken only for online
					credentialType = (AuthenticationType == AuthenticationProviderType.OnlineFederation) ? TokenServiceCredentialType.SymmetricToken : _tokenEndpointType;
				}
				else
				{
					credentialType = TokenServiceCredentialType.Kerberos;
				}

				authEndpoint = IssuerEndpoints.GetIssuerEndpoint(credentialType);
				if (authEndpoint != null)
				{
					lock (_lockObject)
					{
						CurrentServiceEndpoint.Binding = ServiceMetadataUtility.SetIssuer(CurrentServiceEndpoint.Binding, authEndpoint);
					}
				}
			}

			var factory = CreateLocalChannelFactory();
			factory.Credentials.SetSupportInteractive(false);

			return factory;
		}

		[SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", Justification = "Value is returned from method and cannot be disposed.")]
		public ChannelFactory<TService> CreateChannelFactory(ClientCredentials clientCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(CurrentServiceEndpoint, "CurrentServiceEndpoint");

			// We can't check for the client credentials in order not to throw with Legacy RPS
			if (ClaimsEnabledService)
			{
				TokenServiceCredentialType credentialType = GetCredentialsEndpointType(clientCredentials);

				var authEndpoint = IssuerEndpoints.GetIssuerEndpoint(credentialType);
				if (authEndpoint != null)
				{
					lock (_lockObject)
					{
						CurrentServiceEndpoint.Binding = ServiceMetadataUtility.SetIssuer(CurrentServiceEndpoint.Binding, authEndpoint);
					}
				}
			}

			var factory = CreateLocalChannelFactory();

			ConfigureCredentials(factory, clientCredentials);

#if NETFRAMEWORK
			factory.Credentials.SetSupportInteractive(clientCredentials != null ? clientCredentials.GetSupportInteractive() : false);
#endif
			return factory;
		}

#region Authenticate Cross Realm

		/// <summary>
		/// Authenticates based on the client credentials passed in.
		/// </summary>
		/// <param name="clientCredentials">The standard ClientCredentials</param>
		/// <param name="appliesTo"></param>
		/// <param name="crossRealmSts"></param>
		/// <returns>RequestSecurityTokenResponse</returns>
		public SecurityTokenResponse AuthenticateCrossRealm(ClientCredentials clientCredentials, string appliesTo, Uri crossRealmSts)
		{
			if (crossRealmSts != null)
			{
				AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
				authenticationCredentials.AppliesTo = !string.IsNullOrWhiteSpace(appliesTo) ? new Uri(appliesTo) : null;
				authenticationCredentials.KeyType = string.Empty;

				authenticationCredentials.ClientCredentials = clientCredentials;
				authenticationCredentials.SecurityTokenResponse = null;
				IdentityProviderTrustConfiguration idp = TryGetOnlineTrustConfiguration(crossRealmSts);
				authenticationCredentials.EndpointType = idp != null ? TokenServiceCredentialType.Username : GetCredentialsEndpointType(clientCredentials);
				authenticationCredentials.IssuerEndpoints = CrossRealmIssuerEndpoints[crossRealmSts];

				if (this.AuthenticationType == AuthenticationProviderType.OnlineFederation && idp == null)
				{
					authenticationCredentials.KeyType = KeyTypes.Bearer;
				}

				return AuthenticateInternal(authenticationCredentials);
			}

			return null;
		}

		/// <summary>
		/// Authenticates based on the security token passed in.
		/// </summary>
		/// <param name="securityToken"></param>
		/// <param name="appliesTo"></param>
		/// <param name="crossRealmSts"></param>
		/// <returns>RequestSecurityTokenResponse</returns>
		public SecurityTokenResponse AuthenticateCrossRealm(SecurityToken securityToken, string appliesTo, Uri crossRealmSts)
		{
			if (crossRealmSts != null)
			{
				AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
				authenticationCredentials.AppliesTo = !string.IsNullOrWhiteSpace(appliesTo) ? new Uri(appliesTo) : null;

				authenticationCredentials.KeyType = string.Empty;

				authenticationCredentials.ClientCredentials = null;
				authenticationCredentials.SecurityTokenResponse = new SecurityTokenResponse() { Token = securityToken };
				bool useDefaultTokenType = true;
				if (AuthenticationType == AuthenticationProviderType.OnlineFederation)
				{
					IdentityProviderTrustConfiguration idp = TryGetOnlineTrustConfiguration(crossRealmSts);
					if (idp != null)
					{
						if (idp.Endpoint.GetServiceRoot() == crossRealmSts)
						{
							authenticationCredentials.EndpointType = TokenServiceCredentialType.SymmetricToken;
							useDefaultTokenType = false;
						}
					}
				}

				if (useDefaultTokenType)
				{
					authenticationCredentials.EndpointType = _tokenEndpointType;
				}

				authenticationCredentials.IssuerEndpoints = CrossRealmIssuerEndpoints[crossRealmSts];
				return AuthenticateInternal(authenticationCredentials);
			}

			return null;
		}

#endregion Authenticate Cross Realm

#region OrgID

		private IdentityProviderTrustConfiguration TryGetOnlineTrustConfiguration()
		{
			var liveConfiguration = PolicyConfiguration as OnlinePolicyConfiguration;
			if (liveConfiguration == null)
			{
				return null;
			}

			return liveConfiguration.OnlineProviders.Values.OfType<OrgIdentityProviderTrustConfiguration>().FirstOrDefault();
		}

		private IdentityProviderTrustConfiguration GetLiveTrustConfig<T>()
			where T : IdentityProviderTrustConfiguration
		{
			var liveConfiguration = PolicyConfiguration as OnlinePolicyConfiguration;
			ClientExceptionHelper.ThrowIfNull(liveConfiguration, "liveConfiguration");

			IdentityProviderTrustConfiguration liveTrustConfig = liveConfiguration.OnlineProviders.Values.OfType<T>().FirstOrDefault();

			ClientExceptionHelper.ThrowIfNull(liveTrustConfig, "liveTrustConfig");
			return liveTrustConfig;
		}

		private IdentityProviderTrustConfiguration GetOnlineTrustConfiguration(Uri crossRealmSts)
		{
			var liveFederationConfiguration = PolicyConfiguration as OnlineFederationPolicyConfiguration;

			ClientExceptionHelper.ThrowIfNull(liveFederationConfiguration, "liveFederationConfiguration");

			if (liveFederationConfiguration.OnlineProviders.ContainsKey(crossRealmSts))
			{
				return liveFederationConfiguration.OnlineProviders[crossRealmSts];
			}

			return null;
		}

		private IdentityProviderTrustConfiguration TryGetOnlineTrustConfiguration(Uri crossRealmSts)
		{
			var liveFederationConfiguration = PolicyConfiguration as OnlineFederationPolicyConfiguration;

			if (liveFederationConfiguration != null && liveFederationConfiguration.OnlineProviders.ContainsKey(crossRealmSts))
			{
				return liveFederationConfiguration.OnlineProviders[crossRealmSts];
			}

			return null;
		}
#endregion OrgID

#region Authenticate ClientCredentials

		/// <summary>
		/// Authenticates based on the client credentials passed in.
		/// </summary>
		/// <param name="clientCredentials">The standard ClientCredentials</param>
		/// <returns>RequestSecurityTokenResponse</returns>
		public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials)
		{
			if (CurrentServiceEndpoint != null)
			{
				AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
				authenticationCredentials.ClientCredentials = clientCredentials;
				var returnCrededentials = Authenticate(authenticationCredentials);
				if (returnCrededentials != null && returnCrededentials.SecurityTokenResponse != null)
				{
					return returnCrededentials.SecurityTokenResponse;
				}
			}

			return null;
		}

		/// <summary>
		/// Authenticates based on the client credentials passed in.
		/// </summary>
		/// <param name="clientCredentials"></param>
		/// <param name="uri"></param>
		/// <param name="keyType">Optional.  Can be set to Bearer if bearer token required</param>
		/// <returns>RequestSecurityTokenResponse</returns>
		internal SecurityTokenResponse Authenticate(ClientCredentials clientCredentials, Uri uri, string keyType)
		{
			AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
			authenticationCredentials.AppliesTo = uri;
			authenticationCredentials.EndpointType = GetCredentialsEndpointType(clientCredentials);
			authenticationCredentials.KeyType = keyType;
			authenticationCredentials.IssuerEndpoints = IssuerEndpoints;
			authenticationCredentials.ClientCredentials = clientCredentials;
			authenticationCredentials.SecurityTokenResponse = null;
			return AuthenticateInternal(authenticationCredentials);
		}
#endregion Authenticate ClientCredentials

#region Authenticate SecurityToken

		/// <summary>
		/// Authenticates based on the security token passed in.
		/// </summary>
		/// <param name="securityToken"></param>
		/// <returns>RequestSecurityTokenResponse</returns>
		public SecurityTokenResponse Authenticate(SecurityToken securityToken)
		{
			ClientExceptionHelper.ThrowIfNull(securityToken, "securityToken");

			if (AuthenticationType == AuthenticationProviderType.OnlineFederation)
			{
				var liveTrustConfig = TryGetOnlineTrustConfiguration();
				if (liveTrustConfig == null)
				{
					return null;
				}

				return AuthenticateCrossRealm(securityToken, liveTrustConfig.AppliesTo, liveTrustConfig.Endpoint.GetServiceRoot());
			}

			if (CurrentServiceEndpoint != null)
			{
				AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
				authenticationCredentials.AppliesTo = CurrentServiceEndpoint.Address.Uri;
				authenticationCredentials.EndpointType = _tokenEndpointType;
				authenticationCredentials.KeyType = string.Empty;
				authenticationCredentials.IssuerEndpoints = IssuerEndpoints;
				authenticationCredentials.ClientCredentials = null;
				authenticationCredentials.SecurityTokenResponse = new SecurityTokenResponse() { Token = securityToken };

				return AuthenticateInternal(authenticationCredentials);
			}

			return null;
		}

		/// <summary>
		/// Authenticates based on the security token passed in.
		/// </summary>
		/// <param name="securityToken"></param>
		/// <param name="uri"></param>
		/// <param name="keyType">Optional.  Can be set to Bearer if bearer token required</param>
		/// <returns>RequestSecurityTokenResponse</returns>
		internal SecurityTokenResponse Authenticate(SecurityToken securityToken, Uri uri, string keyType)
		{
			ClientExceptionHelper.ThrowIfNull(securityToken, "securityToken");

			if (uri != null)
			{
				AuthenticationCredentials authenticationCredentials = new AuthenticationCredentials();
				authenticationCredentials.AppliesTo = uri.GetServiceRoot();
				authenticationCredentials.EndpointType = _tokenEndpointType;
				authenticationCredentials.KeyType = keyType;
				authenticationCredentials.IssuerEndpoints = IssuerEndpoints;
				authenticationCredentials.ClientCredentials = null;
				authenticationCredentials.SecurityTokenResponse = new SecurityTokenResponse() { Token = securityToken };
				return AuthenticateInternal(authenticationCredentials);
			}

			return null;
		}
#endregion Authenticate SecurityToken

#region Authenticate LiveId

		/// <summary>
		/// This will default to LiveID auth when on-line.
		/// </summary>
		/// <param name="clientCredentials"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		public SecurityTokenResponse AuthenticateDevice(ClientCredentials clientCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(clientCredentials, "clientCredentials");

			throw new InvalidOperationException("Authentication to MSA services is not supported.");
		}

		/// <summary>
		/// This will default to LiveID auth when on-line.
		/// </summary>
		/// <param name="clientCredentials"></param>
		/// <param name="deviceTokenResponse"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials, SecurityTokenResponse deviceTokenResponse)
		{
			ClientExceptionHelper.ThrowIfNull(clientCredentials, "clientCredentials");
			ClientExceptionHelper.ThrowIfNull(deviceTokenResponse, "deviceTokenResponse");

			throw new InvalidOperationException("Authentication to MSA services is not supported.");
		}

		/// <summary>
		/// This will default to LiveID auth when on-line.
		/// </summary>
		/// <param name="clientCredentials"></param>
		/// <param name="deviceTokenResponse"></param>
		/// <param name="keyType"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		public SecurityTokenResponse Authenticate(ClientCredentials clientCredentials, SecurityTokenResponse deviceTokenResponse, string keyType)
		{
			ClientExceptionHelper.ThrowIfNull(clientCredentials, "clientCredentials");
			ClientExceptionHelper.ThrowIfNull(deviceTokenResponse, "deviceTokenResponse");
			ClientExceptionHelper.ThrowIfNullOrEmpty(keyType, "keyType");

			throw new InvalidOperationException("Authentication to MSA services is not supported.");
		}

		public IdentityProvider GetIdentityProvider(string userPrincipalName)
		{
			IdentityProviderTrustConfiguration idp = TryGetOnlineTrustConfiguration();
			if (idp == null)
			{
				return null;
			}

			return IdentityProviderLookup.Instance.GetIdentityProvider(idp.Endpoint.GetServiceRoot(), idp.Endpoint.GetServiceRoot(), userPrincipalName);
		}

#endregion Authenticate LiveId

		private SecurityTokenResponse AuthenticateInternal(AuthenticationCredentials authenticationCredentials)
		{
			ClientExceptionHelper.Assert(this.AuthenticationType == AuthenticationProviderType.Federation || this.AuthenticationType == AuthenticationProviderType.OnlineFederation, "Authenticate is not supported when not in claims mode!");

			if (ClaimsEnabledService)
			{
				if (authenticationCredentials.IssuerEndpoint.CredentialType == TokenServiceCredentialType.Kerberos)
				{
					bool retry = false;
					int retryCount = 0;
					do
					{
						try
						{
							return Issue(authenticationCredentials);
						}
						catch (SecurityTokenValidationException)
						{
							retry = false;

							// Fall back to windows integrated.
							if (authenticationCredentials.IssuerEndpoints.ContainsKey(TokenServiceCredentialType.Windows.ToString()))
							{
								authenticationCredentials.EndpointType = TokenServiceCredentialType.Windows;
								retry = ++retryCount < 2;
							}

							// We don't care, we just want to return null.  The reason why we are are catching this one is because in pure Kerberos mode, this
							// will throw a very bad exception that will crash VS.
						}
						catch (SecurityNegotiationException)
						{
							// This is the exception with Integrated Windows Auth.
							// We don't care, we just want to return null.  The reason why we are are catching this one is because in pure Kerberos mode, this
							// will throw a very bad exception that will crash VS.
							retry = ++retryCount < 2;
						}
						catch (FaultException)
						{
							// Fall back to windows integrated.
							if (authenticationCredentials.IssuerEndpoints.ContainsKey(TokenServiceCredentialType.Windows.ToString()))
							{
								authenticationCredentials.EndpointType = TokenServiceCredentialType.Windows;
								retry = ++retryCount < 2;
							}
						}
					}
					while (retry);
				}
				else
				{
					return Issue(authenticationCredentials);
				}
			}

			return null;
		}

		/// <summary>
		/// This is the method that actually creates the trust channel factory and issues the request for the token.
		/// </summary>
		/// <returns></returns>
		[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "[WSTrustChannelFactory] Pending resolution of bug: https://dev.azure.com/dynamicscrm/OneCRM/_workitems/edit/2493634")]
		private SecurityTokenResponse Issue(AuthenticationCredentials authenticationCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(authenticationCredentials, "authenticationCredentials");
			ClientExceptionHelper.ThrowIfNull(authenticationCredentials.IssuerEndpoint, "authenticationCredentials.IssuerEndpoint");
			ClientExceptionHelper.ThrowIfNull(authenticationCredentials.AppliesTo, "authenticationCredentials.AppliesTo");

#if NETFRAMEWORK
			WSTrustChannelFactory channelFactory = null;
			WSTrustChannel channel = null;
			try
			{
				authenticationCredentials.RequestType = RequestTypes.Issue;

				channelFactory = new WSTrustChannelFactory(authenticationCredentials.IssuerEndpoint.IssuerBinding, authenticationCredentials.IssuerEndpoint.IssuerAddress);

				var supportingToken = (authenticationCredentials.SecurityTokenResponse != null && authenticationCredentials.SecurityTokenResponse.Token != null) ? authenticationCredentials.SecurityTokenResponse.Token :
							 (authenticationCredentials.SupportingCredentials != null && authenticationCredentials.SupportingCredentials.SecurityTokenResponse != null && authenticationCredentials.SupportingCredentials.SecurityTokenResponse.Token != null) ? authenticationCredentials.SupportingCredentials.SecurityTokenResponse.Token : null;

				if (supportingToken != null)
				{
					channelFactory.Credentials.SupportInteractive = false;
				}
				else
				{
					ConfigureCredentials(channelFactory, authenticationCredentials.ClientCredentials);
				}

				channel = supportingToken != null
							? (WSTrustChannel)channelFactory.CreateChannelWithIssuedToken(supportingToken)
							: (WSTrustChannel)channelFactory.CreateChannel();

				if (channel != null)
				{
					var rst = new RequestSecurityToken(authenticationCredentials.RequestType)
					{
						AppliesTo = new EndpointReference(authenticationCredentials.AppliesTo.AbsoluteUri)
					};

					if (!string.IsNullOrEmpty(authenticationCredentials.KeyType))
					{
						rst.KeyType = authenticationCredentials.KeyType;
					}

					RequestSecurityTokenResponse rstr;
					var token = channel.Issue(rst, out rstr);
					return new SecurityTokenResponse() { Token = token, Response = rstr, EndpointType = authenticationCredentials.EndpointType };
				}
			}
			finally
			{
				if (channel != null)
				{
					channel.Close(true);
				}

				channel = null;
				if (channelFactory != null)
				{
					channelFactory.Close(true);
				}

				channelFactory = null;
			}

			return null;
#else
			throw new PlatformNotSupportedException("Xrm.Sdk WSTrust");
#endif
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		private void ConfigureCredentials(ChannelFactory channelFactory, ClientCredentials clientCredentials)
		{
			if (clientCredentials != null)
			{
				if (clientCredentials.ClientCertificate != null && clientCredentials.ClientCertificate.Certificate != null)
				{
					channelFactory.Credentials.ClientCertificate.Certificate = clientCredentials.ClientCertificate.Certificate;
				}
				else if (clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName))
				{
					channelFactory.Credentials.UserName.UserName = clientCredentials.UserName.UserName;
					channelFactory.Credentials.UserName.Password = clientCredentials.UserName.Password;
				}
				else if (clientCredentials.Windows != null && (clientCredentials.Windows.ClientCredential != null))
				{
					channelFactory.Credentials.Windows.ClientCredential = clientCredentials.Windows.ClientCredential;
					channelFactory.Credentials.Windows.AllowedImpersonationLevel = clientCredentials.Windows.AllowedImpersonationLevel;
				}

				// We don't want to do anything specific so that the default credential searching can be done.
			}
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		private TokenServiceCredentialType GetCredentialsEndpointType(ClientCredentials clientCredentials)
		{
			if (clientCredentials != null)
			{
				if (clientCredentials.UserName != null && !string.IsNullOrEmpty(clientCredentials.UserName.UserName))
				{
					return TokenServiceCredentialType.Username;
				}

				if (clientCredentials.ClientCertificate != null && clientCredentials.ClientCertificate.Certificate != null)
				{
					return TokenServiceCredentialType.Certificate;
				}

				if (clientCredentials.Windows != null && (clientCredentials.Windows.ClientCredential != null))
				{
					return TokenServiceCredentialType.Kerberos;
				}

				// We don't want to do anything specific so that the default credential searching can be done.
			}

			return TokenServiceCredentialType.Kerberos;
		}

		[SuppressMessage("Microsoft.Usage", "CA9888:DisposeObjectsCorrectly", Justification = "Value is returned from method and cannot be disposed.")]
		private ChannelFactory<TService> CreateLocalChannelFactory()
		{
#if NETFRAMEWORK
			lock (_lockObject)
			{
				ServiceEndpoint endpoint = new ServiceEndpoint(CurrentServiceEndpoint.Contract, CurrentServiceEndpoint.Binding, CurrentServiceEndpoint.Address);
				foreach (var behavior in CurrentServiceEndpoint.Behaviors)
				{
					endpoint.Behaviors.Add(behavior);
				}

				endpoint.IsSystemEndpoint = CurrentServiceEndpoint.IsSystemEndpoint;
				endpoint.ListenUri = CurrentServiceEndpoint.ListenUri;
				endpoint.ListenUriMode = CurrentServiceEndpoint.ListenUriMode;
				endpoint.Name = CurrentServiceEndpoint.Name;

				var factory = new ChannelFactory<TService>(endpoint);

				factory.Credentials.IssuedToken.CacheIssuedTokens = true;
				return factory;
			}
#else
			lock (_lockObject)
			{
				ServiceEndpoint endpoint = new ServiceEndpoint(CurrentServiceEndpoint.Contract, CurrentServiceEndpoint.Binding, CurrentServiceEndpoint.Address);
				foreach (var behavior in CurrentServiceEndpoint.EndpointBehaviors)
				{
					endpoint.EndpointBehaviors.Add(behavior);
				}
				endpoint.Name = CurrentServiceEndpoint.Name;

				var factory = new ChannelFactory<TService>(endpoint);
				return factory;
			}

			throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif
		}

		private static object _lockObject = new object();
#region Constants
		internal const string DefaultRequestType = RequestTypes.Issue;
#endregion Constants
	}
}
