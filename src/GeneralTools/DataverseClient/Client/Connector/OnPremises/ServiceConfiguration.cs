using System;
using System.IdentityModel.Protocols.WSTrust;
using System.Linq;
using System.ServiceModel.Description;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;


namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
	internal sealed partial class ServiceConfiguration<TService>
	{
		#region IServiceManagement
		public AuthenticationCredentials Authenticate(AuthenticationCredentials authenticationCredentials)
		{
            ClientExceptionHelper.ThrowIfNull(authenticationCredentials, "authenticationCredentials");

			switch (AuthenticationType)
			{
				case AuthenticationProviderType.OnlineFederation:
					return AuthenticateOnlineFederationInternal(authenticationCredentials);
				case AuthenticationProviderType.Federation:
					return AuthenticateFederationInternal(authenticationCredentials);
				case AuthenticationProviderType.ActiveDirectory:
					{
						ServiceMetadataUtility.AdjustUserNameForWindows(authenticationCredentials.ClientCredentials);
						return authenticationCredentials;
					}

				default:
					return authenticationCredentials;
			}
		}

		private AuthenticationCredentials AuthenticateFederationInternal(AuthenticationCredentials authenticationCredentials)
		{
			if (authenticationCredentials.SecurityTokenResponse != null)
			{
				return AuthenticateFederationTokenInternal(authenticationCredentials);
			}

			if (authenticationCredentials.AppliesTo == null)
			{
				authenticationCredentials.AppliesTo = CurrentServiceEndpoint.Address.Uri;
			}

			authenticationCredentials.EndpointType = GetCredentialsEndpointType(authenticationCredentials.ClientCredentials);

			authenticationCredentials.IssuerEndpoints = authenticationCredentials.HomeRealm != null ? CrossRealmIssuerEndpoints[authenticationCredentials.HomeRealm] : IssuerEndpoints;
			authenticationCredentials.SecurityTokenResponse = AuthenticateInternal(authenticationCredentials);

			return authenticationCredentials;
		}

		private AuthenticationCredentials AuthenticateFederationTokenInternal(AuthenticationCredentials authenticationCredentials)
		{
			AuthenticationCredentials returnCredentials = new AuthenticationCredentials();
			returnCredentials.SupportingCredentials = authenticationCredentials;
			if (authenticationCredentials.AppliesTo == null)
			{
				authenticationCredentials.AppliesTo = CurrentServiceEndpoint.Address.Uri;
			}

			authenticationCredentials.EndpointType = _tokenEndpointType;
			authenticationCredentials.KeyType = string.Empty;
			authenticationCredentials.IssuerEndpoints = IssuerEndpoints;

			returnCredentials.SecurityTokenResponse = AuthenticateInternal(authenticationCredentials);

			return returnCredentials;
		}

		/// <summary>
		/// Supported matrix:
		/// 1.  Security Token Response populated: We will submit the token to Org ID to exchange for a CRM token.
		/// 2.  Credentials passed.  
		/// 		a.  The UserPrincipalName MUST be populated if the Username/Windows username is empty AND the Home Realm Uri is null.
		/// 		a.  If the Home Realm 
		/// </summary>
		/// <param name="authenticationCredentials"></param>
		/// <returns></returns>
		private AuthenticationCredentials AuthenticateOnlineFederationInternal(AuthenticationCredentials authenticationCredentials)
		{
			var onlinePolicy = PolicyConfiguration as OnlinePolicyConfiguration;
			ClientExceptionHelper.ThrowIfNull(onlinePolicy, "onlinePolicy");

			OrgIdentityProviderTrustConfiguration liveTrustConfig = onlinePolicy.OnlineProviders.Values.OfType<OrgIdentityProviderTrustConfiguration>().FirstOrDefault();
			ClientExceptionHelper.ThrowIfNull(liveTrustConfig, "liveTrustConfig");

			// Two scenarios:
			// 1.  Managed Credentials
			// 2.  Federated Credentials
			// 3.  A token to submit to OrgID.
			if (authenticationCredentials.SecurityTokenResponse != null)
			{
				return AuthenticateOnlineFederationTokenInternal(liveTrustConfig, authenticationCredentials);
			}

			bool authWithOrgId = true;

			if (authenticationCredentials.HomeRealm == null)
			{
				IdentityProvider identityProvider = !string.IsNullOrEmpty(authenticationCredentials.UserPrincipalName) ? GetIdentityProvider(authenticationCredentials.UserPrincipalName) : GetIdentityProvider(authenticationCredentials.ClientCredentials);
				ClientExceptionHelper.ThrowIfNull(identityProvider, "identityProvider");
				authenticationCredentials.HomeRealm = identityProvider.ServiceUrl;
				authWithOrgId = identityProvider.IdentityProviderType == IdentityProviderType.OrgId;
				if (authWithOrgId)
				{
					ClientExceptionHelper.Assert(onlinePolicy.OnlineProviders.ContainsKey(authenticationCredentials.HomeRealm), "Online Identity Provider NOT found!  {0}", identityProvider.ServiceUrl);
				}
			}

			authenticationCredentials.AppliesTo = new Uri(liveTrustConfig.AppliesTo);
			authenticationCredentials.IssuerEndpoints = this.IssuerEndpoints;
			authenticationCredentials.KeyType = KeyTypes.Bearer;
			authenticationCredentials.EndpointType = TokenServiceCredentialType.Username;

			if (authWithOrgId)
			{
				return AuthenticateTokenWithOrgIdForCrm(authenticationCredentials);
			}

			// Authenticate with ADFS to retrieve a token for OrgId
			AuthenticationCredentials adfsCredentials = AuthenticateWithADFSForOrgId(authenticationCredentials, liveTrustConfig.Identifier);

			return AuthenticateFederatedTokenWithOrgIdForCRM(adfsCredentials);
		}

		/// <summary>
		/// Authenticates a federated token with OrgID to retrieve a token for CRM
		/// </summary>
		/// <param name="authenticationCredentials"></param>
		private AuthenticationCredentials AuthenticateFederatedTokenWithOrgIdForCRM(AuthenticationCredentials authenticationCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(authenticationCredentials, "authenticationCredentials");

			ClientExceptionHelper.ThrowIfNull(authenticationCredentials.SecurityTokenResponse, "authenticationCredentials.SecurityTokenResponse");

			AuthenticationCredentials returnCredentials = new AuthenticationCredentials();
			returnCredentials.SupportingCredentials = authenticationCredentials;
			returnCredentials.AppliesTo = authenticationCredentials.AppliesTo;
			returnCredentials.IssuerEndpoints = authenticationCredentials.IssuerEndpoints;
			returnCredentials.EndpointType = TokenServiceCredentialType.SymmetricToken;
			returnCredentials.SecurityTokenResponse = AuthenticateInternal(returnCredentials);
			return returnCredentials;
		}

		/// <summary>
		/// Authenticates with ADFS to retrieve a federated token to exchange with OrgId for CRM
		/// </summary>
		/// <param name="authenticationCredentials"></param>
		/// <param name="identifier"></param>
		private AuthenticationCredentials AuthenticateWithADFSForOrgId(AuthenticationCredentials authenticationCredentials, Uri identifier)
		{
			AuthenticationCredentials returnCredentials = new AuthenticationCredentials();
			returnCredentials.AppliesTo = authenticationCredentials.AppliesTo;
			returnCredentials.SupportingCredentials = authenticationCredentials;
			returnCredentials.AppliesTo = authenticationCredentials.AppliesTo;
			returnCredentials.IssuerEndpoints = authenticationCredentials.IssuerEndpoints;
			returnCredentials.EndpointType = TokenServiceCredentialType.SymmetricToken;

			// We are authenticating against ADFS with the credentials.
			authenticationCredentials.AppliesTo = identifier;
			authenticationCredentials.KeyType = KeyTypes.Bearer;
			authenticationCredentials.EndpointType = GetCredentialsEndpointType(authenticationCredentials.ClientCredentials);
			authenticationCredentials.IssuerEndpoints = CrossRealmIssuerEndpoints[authenticationCredentials.HomeRealm];

			returnCredentials.SecurityTokenResponse = AuthenticateInternal(authenticationCredentials);

			return returnCredentials;
		}

		private AuthenticationCredentials AuthenticateTokenWithOrgIdForCrm(AuthenticationCredentials authenticationCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(authenticationCredentials, "authenticationCredentials");

			AuthenticationCredentials returnAcsCredentials = new AuthenticationCredentials();
			returnAcsCredentials.SupportingCredentials = authenticationCredentials;
			returnAcsCredentials.AppliesTo = authenticationCredentials.AppliesTo;
			returnAcsCredentials.IssuerEndpoints = authenticationCredentials.IssuerEndpoints;
			returnAcsCredentials.KeyType = KeyTypes.Bearer;
			returnAcsCredentials.EndpointType = TokenServiceCredentialType.Username;

			returnAcsCredentials.SecurityTokenResponse = AuthenticateInternal(authenticationCredentials);
			return returnAcsCredentials;
		}

		private AuthenticationCredentials AuthenticateOnlineFederationTokenInternal(IdentityProviderTrustConfiguration liveTrustConfig, AuthenticationCredentials authenticationCredentials)
		{
			AuthenticationCredentials returnCredentials = new AuthenticationCredentials();
			returnCredentials.SupportingCredentials = authenticationCredentials;

			string appliesTo = authenticationCredentials.AppliesTo != null ? authenticationCredentials.AppliesTo.AbsoluteUri : liveTrustConfig.AppliesTo;
			Uri tokenEndpoint = authenticationCredentials.HomeRealm ?? liveTrustConfig.Endpoint.GetServiceRoot();

			returnCredentials.SecurityTokenResponse = AuthenticateCrossRealm(authenticationCredentials.SecurityTokenResponse.Token, appliesTo, tokenEndpoint);

			return returnCredentials;
		}

		#endregion IServiceManagement

		internal IdentityProvider GetIdentityProvider(ClientCredentials clientCredentials)
		{
			string userName = string.Empty;

			if (!string.IsNullOrWhiteSpace(clientCredentials.UserName.UserName))
			{
				userName = ExtractUserName(clientCredentials.UserName.UserName);
			}
			else if (!string.IsNullOrWhiteSpace(clientCredentials.Windows.ClientCredential.UserName))
			{
				userName = ExtractUserName(clientCredentials.Windows.ClientCredential.UserName);
			}

			ClientExceptionHelper.Assert(!string.IsNullOrEmpty(userName), "clientCredentials.UserName.UserName or clientCredentials.Windows.ClientCredential.UserName MUST be populated!");
			return GetIdentityProvider(userName);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
		private string ExtractUserName(string userName)
		{
			return userName.Contains('@') ? userName : string.Empty;
		}
	}
}
