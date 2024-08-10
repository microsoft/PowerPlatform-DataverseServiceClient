using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens;
using System.IO;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.ServiceModel.Security.Tokens;
using System.Text;
using System.Xml;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Common;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
	/// <summary>
	/// 	Handles retrieving/making use of service metadata information.
	/// </summary>
	internal static class ServiceMetadataUtility
	{
		public static IssuerEndpointDictionary RetrieveIssuerEndpoints(EndpointAddress issuerMetadataAddress)
		{
#if NETFRAMEWORK
			var alternateIssuers = new IssuerEndpointDictionary();

			var mcli = CreateMetadataClient(issuerMetadataAddress.Uri.Scheme);
			MetadataSet stsMDS = mcli.GetMetadata(issuerMetadataAddress.Uri, MetadataExchangeClientMode.HttpGet);

			if (stsMDS != null)
			{
				var stsImporter = new WsdlImporter(stsMDS);
				var stsEndpoints = stsImporter.ImportAllEndpoints();

				foreach (var stsEndpoint in stsEndpoints)
				{
					if (!(stsEndpoint.Binding is NetTcpBinding))
					{
						var credentialType = TokenServiceCredentialType.None;
						TrustVersion endpointTrustVersion = TrustVersion.Default;

						var wsHttpBinding = stsEndpoint.Binding as WS2007HttpBinding;
						if (wsHttpBinding != null)
						{
							var elements = wsHttpBinding.CreateBindingElements();
							var securityElement = elements.Find<SecurityBindingElement>();
							if (securityElement != null)
							{
								endpointTrustVersion = securityElement.MessageSecurityVersion.TrustVersion;
								if (endpointTrustVersion == TrustVersion.WSTrust13)
								{
									if (wsHttpBinding.Security.Message.ClientCredentialType == MessageCredentialType.UserName)
									{
										credentialType = TokenServiceCredentialType.Username;
									}
									else if (wsHttpBinding.Security.Message.ClientCredentialType == MessageCredentialType.Certificate)
									{
										credentialType = TokenServiceCredentialType.Certificate;
									}
									else if (wsHttpBinding.Security.Message.ClientCredentialType == MessageCredentialType.Windows)
									{
										credentialType = TokenServiceCredentialType.Windows;
									}
								}
							}
						}
						else
						{
							// We need to do a little deeper look to figure out what we need.	
							var elements = stsEndpoint.Binding.CreateBindingElements();
							var securityElement = elements.Find<SecurityBindingElement>();
							if (securityElement != null)
							{
								endpointTrustVersion = securityElement.MessageSecurityVersion.TrustVersion;

								if (endpointTrustVersion == TrustVersion.WSTrust13)
								{
									var issuedTokenParameters = GetIssuedTokenParameters(securityElement);
									if (issuedTokenParameters != null)
									{
										if (issuedTokenParameters.KeyType == SecurityKeyType.SymmetricKey)
										{
											credentialType = TokenServiceCredentialType.SymmetricToken;
										}
										else if (issuedTokenParameters.KeyType == SecurityKeyType.AsymmetricKey)
										{
											credentialType = TokenServiceCredentialType.AsymmetricToken;
										}
										else if (issuedTokenParameters.KeyType == SecurityKeyType.BearerKey)
										{
											credentialType = TokenServiceCredentialType.Bearer;
										}
									}
									else
									{
										var kerberosTokenParameters = GetKerberosTokenParameters(securityElement);
										if (kerberosTokenParameters != null)
										{
											credentialType = TokenServiceCredentialType.Kerberos;
										}
									}
								}
								else
								{
									// We only support 2005 for MFG
								}
							}
						}

						if (credentialType != TokenServiceCredentialType.None)
						{
							var endpointKey = credentialType.ToString();
							if (!alternateIssuers.ContainsKey(endpointKey))
							{
								alternateIssuers.Add(endpointKey,
													 new IssuerEndpoint
													 {
														 IssuerAddress = stsEndpoint.Address,
														 IssuerBinding = stsEndpoint.Binding,
														 IssuerMetadataAddress = issuerMetadataAddress,
														 CredentialType = credentialType,
														 TrustVersion = endpointTrustVersion,
													 });
							}
						}
					}
				}
			}

			return alternateIssuers;
#else
			throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2141:TransparentMethodsMustNotSatisfyLinkDemandsFxCopRule")]
		public static IssuerEndpointDictionary RetrieveLiveIdIssuerEndpoints(IdentityProviderTrustConfiguration identityProviderTrustConfiguration)
		{
#if NETFRAMEWORK
			var issuers = new IssuerEndpointDictionary();

			issuers.Add(TokenServiceCredentialType.Username.ToString(),
						new IssuerEndpoint
						{
							CredentialType = TokenServiceCredentialType.Username,
							IssuerAddress = new EndpointAddress(identityProviderTrustConfiguration.Endpoint.AbsoluteUri),
							IssuerBinding = identityProviderTrustConfiguration.Binding
						});
			var stsBinding = new Microsoft.Crm.Protocols.WSTrust.Bindings.IssuedTokenWSTrustBinding()
			{
				TrustVersion = identityProviderTrustConfiguration.TrustVersion,
				SecurityMode = identityProviderTrustConfiguration.SecurityMode
			};
			stsBinding.KeyType = SecurityKeyType.BearerKey;
			issuers.Add(TokenServiceCredentialType.SymmetricToken.ToString(),
						new IssuerEndpoint
						{
							CredentialType = TokenServiceCredentialType.SymmetricToken,
							IssuerAddress = new EndpointAddress(identityProviderTrustConfiguration.Endpoint.AbsoluteUri),
							IssuerBinding = stsBinding
						});
			return issuers;
#else
			throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif
		}

		public static IssuerEndpointDictionary RetrieveDefaultIssuerEndpoint(AuthenticationProviderType authenticationProviderType, IssuerEndpoint issuer)
		{
			var issuers = new IssuerEndpointDictionary();

			if (issuer != null && issuer.IssuerAddress != null)
			{
				// Default to username
				TokenServiceCredentialType credentialType;

				// Go ahead and add the auth endpoint.  We'll assume username for now, since we have nothing to go on.
				switch (authenticationProviderType)
				{
					case AuthenticationProviderType.Federation:
						credentialType = TokenServiceCredentialType.Kerberos;
						break;
					case AuthenticationProviderType.OnlineFederation:
						credentialType = TokenServiceCredentialType.Username;
						break;
					default:
						credentialType = TokenServiceCredentialType.Kerberos;
						break;
				}

				issuers.Add(credentialType.ToString(),
							new IssuerEndpoint
							{
								CredentialType = credentialType,
								IssuerAddress = issuer.IssuerAddress,
								IssuerBinding = issuer.IssuerBinding
							});
			}

			return issuers;
		}

		[SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We don't care about the actual exception, just want to ignore it for now.")]
		public static IssuerEndpointDictionary RetrieveIssuerEndpoints(AuthenticationProviderType authenticationProviderType, ServiceEndpointDictionary endpoints, bool queryMetadata)
		{
			foreach (var serviceEndpoint in endpoints.Values)
			{
				try
				{
					var issuer = GetIssuer(serviceEndpoint.Binding);

					if (issuer != null)
					{
						if (queryMetadata && issuer.IssuerMetadataAddress != null)
						{
							return RetrieveIssuerEndpoints(issuer.IssuerMetadataAddress);
						}

						// There is no metadata available.  So attempt to add a default one, and allow the calling program to generate missing data.
						return RetrieveDefaultIssuerEndpoint(authenticationProviderType, issuer);
					}
				}
				catch (Exception)
				{
					// We don't care what the exception is at this time.  					
				}
			}

			return new IssuerEndpointDictionary();
		}

		public static IssuerEndpoint GetIssuer(Binding binding)
		{
			if (binding == null)
			{
				return null;
			}

			var elements = binding.CreateBindingElements();
			var securityElement = elements.Find<SecurityBindingElement>();
			var issuerParams = GetIssuedTokenParameters(securityElement);
			if (issuerParams != null)
			{
#if NETFRAMEWORK
				var endpoint = new IssuerEndpoint();
				endpoint.IssuerAddress = issuerParams.IssuerAddress;
				endpoint.IssuerBinding = issuerParams.IssuerBinding;
				endpoint.IssuerMetadataAddress = issuerParams.IssuerMetadataAddress;
				return endpoint;
#else
				throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif
			}

			return null;
        }

#if NETFRAMEWORK
		private static KerberosSecurityTokenParameters GetKerberosTokenParameters(SecurityBindingElement securityElement)
		{
			if (securityElement != null)
			{
				if (securityElement.EndpointSupportingTokenParameters != null)
				{
					if (securityElement.EndpointSupportingTokenParameters.Endorsing != null)
					{
						if (securityElement.EndpointSupportingTokenParameters.Endorsing.Count > 0)
						{
							return securityElement.EndpointSupportingTokenParameters.Endorsing[0] as KerberosSecurityTokenParameters;
						}
					}
				}
			}
			return null;
		}
#endif

        private static IssuedSecurityTokenParameters GetIssuedTokenParameters(SecurityBindingElement securityElement)
		{
			if (securityElement != null)
			{
#if NETFRAMEWORK
				if (securityElement.EndpointSupportingTokenParameters != null)
				{
					if (securityElement.EndpointSupportingTokenParameters.Endorsing != null && securityElement.EndpointSupportingTokenParameters.Endorsing.Count > 0)
					{
						var issuedSecurityTokenParameters = securityElement.EndpointSupportingTokenParameters.Endorsing[0] as IssuedSecurityTokenParameters;
						if (issuedSecurityTokenParameters != null)
						{
							return issuedSecurityTokenParameters;
						}

						var endorsingParam = securityElement.EndpointSupportingTokenParameters.Endorsing[0] as SecureConversationSecurityTokenParameters;
						if (endorsingParam != null)
						{
							// It is possible that there will be more token parameters or in one of the other collections at some point.  For now, we know this is ok.
							if (endorsingParam.BootstrapSecurityBindingElement.EndpointSupportingTokenParameters.Endorsing.Count > 0)
							{
								return endorsingParam.BootstrapSecurityBindingElement.EndpointSupportingTokenParameters.Endorsing[0] as IssuedSecurityTokenParameters;
							}

							if (endorsingParam.BootstrapSecurityBindingElement.EndpointSupportingTokenParameters.Signed.Count > 0)
							{
								return endorsingParam.BootstrapSecurityBindingElement.EndpointSupportingTokenParameters.Signed[0] as IssuedSecurityTokenParameters;
							}
						}
					}
					else if (securityElement.EndpointSupportingTokenParameters.Signed != null && securityElement.EndpointSupportingTokenParameters.Signed.Count > 0)
					{
						// If we have a token parameter here then the server is on-line and behind an NLB.
						return securityElement.EndpointSupportingTokenParameters.Signed[0] as IssuedSecurityTokenParameters;
					}
				}
#else
				throw new PlatformNotSupportedException("Xrm.Sdk WSDL");
#endif
			}

			return null;
		}

		public static CustomBinding SetIssuer(Binding binding, IssuerEndpoint issuerEndpoint)
		{
			var elements = binding.CreateBindingElements();
			var securityElement = elements.Find<SecurityBindingElement>();
			var securityTokenParameters = GetIssuedTokenParameters(securityElement);
			if (securityTokenParameters != null)
			{
#if NETFRAMEWORK
				securityTokenParameters.IssuerAddress = issuerEndpoint.IssuerAddress;
				securityTokenParameters.IssuerBinding = issuerEndpoint.IssuerBinding;
				if (issuerEndpoint.IssuerMetadataAddress != null)
				{
					securityTokenParameters.IssuerMetadataAddress = issuerEndpoint.IssuerMetadataAddress;
				}
#else
				throw new PlatformNotSupportedException("Xrm.Sdk WSTrust");
#endif
			}

			return new CustomBinding(elements);
        }

#if NETFRAMEWORK
		private static void ParseEndpoints(ServiceEndpointDictionary serviceEndpoints, ServiceEndpointCollection serviceEndpointCollection)
		{
			serviceEndpoints.Clear();

			if (serviceEndpointCollection != null)
			{
				foreach (var endpoint in serviceEndpointCollection)
				{
					if (IsEndpointSupported(endpoint))
					{
						serviceEndpoints.Add(endpoint.Name, endpoint);
					}
				}
			}
		}
#endif

        private static bool IsEndpointSupported(ServiceEndpoint endpoint)
		{
			if (endpoint != null)
			{
				// The web endpoints are currently designed for in-browser JScript usage and not by the rich client.
				if (!endpoint.Address.Uri.AbsolutePath.EndsWith(XrmServiceConstants.WebEndpointExtension, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		internal static ServiceEndpointMetadata RetrieveServiceEndpointMetadata(Type contractType, Uri serviceUri, bool checkForSecondary)
		{
#if NETFRAMEWORK // WebInfra; MetadataSet and CreateMetadataClient are NETFRAMEWORK-ONLY
			ServiceEndpointMetadata serviceEndpointMetadata = new ServiceEndpointMetadata();

			serviceEndpointMetadata.ServiceUrls = ServiceConfiguration<IOrganizationService>.CalculateEndpoints(serviceUri);

			if (!checkForSecondary)
			{
				serviceEndpointMetadata.ServiceUrls.AlternateEndpoint = null;
			}

			// Get version of current assembly which is the version of the SDK
			Version sdkVersion = GetSDKVersionNumberFromAssembly();
			var wsdlUri = new Uri(string.Format(CultureInfo.InvariantCulture, "{0}{1}&sdkversion={2}", serviceUri.AbsoluteUri, "?wsdl", sdkVersion.ToString(2)));

			var mcli = CreateMetadataClient(wsdlUri.Scheme);
			if (mcli != null)
			{
				try
				{
					serviceEndpointMetadata.ServiceMetadata = mcli.GetMetadata(wsdlUri, MetadataExchangeClientMode.HttpGet);
		}
				catch (InvalidOperationException ioexp)
				{
					bool rethrow = true;
					if (checkForSecondary)
					{
						var wexp = ioexp.InnerException as WebException;
						if (wexp != null)
						{
							if (wexp.Status == WebExceptionStatus.NameResolutionFailure || wexp.Status == WebExceptionStatus.Timeout)
							{
								if (serviceEndpointMetadata.ServiceUrls != null)
								{
									if (serviceEndpointMetadata.ServiceUrls.PrimaryEndpoint == serviceUri)
									{
										rethrow = TryRetrieveMetadata(mcli, new Uri(serviceEndpointMetadata.ServiceUrls.AlternateEndpoint.AbsoluteUri + "?wsdl"), serviceEndpointMetadata);
									}
									else if (serviceEndpointMetadata.ServiceUrls.AlternateEndpoint == serviceUri)
									{
										rethrow = TryRetrieveMetadata(mcli, new Uri(serviceEndpointMetadata.ServiceUrls.PrimaryEndpoint.AbsoluteUri + "?wsdl"), serviceEndpointMetadata);
									}
								}
							}
						}
					}

					if (rethrow)
					{
						throw;
					}
				}
			}

			ClientExceptionHelper.ThrowIfNull(serviceEndpointMetadata.ServiceMetadata, "STS Metadata");

			var contracts = CreateContractCollection(contractType);

			if (contracts != null)
			{
				// The following code inserts a custom WsdlImporter without removing the other 
				// importers already in the collection.
				var importer = new WsdlImporter(serviceEndpointMetadata.ServiceMetadata);
				var exts = importer.WsdlImportExtensions;

				List<IPolicyImportExtension> policyImportExtensions = AddSecurityBindingToPolicyImporter(importer);

				WsdlImporter stsImporter = new WsdlImporter(serviceEndpointMetadata.ServiceMetadata, policyImportExtensions, exts);

				foreach (ContractDescription description in contracts)
				{
					stsImporter.KnownContracts.Add(GetPortTypeQName(description), description);
				}

				ServiceEndpointCollection endpoints = stsImporter.ImportAllEndpoints();

				if (stsImporter.Errors.Count > 0)
				{
					foreach (MetadataConversionError error in stsImporter.Errors)
					{
						// We can't throw for metadata errors as the initial retrieve will generate a metadata conversion error after querying for the WS-Trust metadata
						// in claims mode since we don't require a particular endpoint.  Why this is considered a fatal error is unclear.
						serviceEndpointMetadata.MetadataConversionErrors.Add(error);
					}
				}

				ParseEndpoints(serviceEndpointMetadata.ServiceEndpoints, endpoints);
			}

			return serviceEndpointMetadata;
#else
            throw new NotImplementedException("ServiceModel metadata support is limited for this target framework");
#endif
		}

            private static Version GetSDKVersionNumberFromAssembly()
		{
			string fileVersion = OrganizationServiceProxy.GetXrmSdkAssemblyFileVersion();

			if (!Version.TryParse(fileVersion, out Version parsedVersion))
			{
				// you need to have major.minor version format, else you will get runtime exception
				// exception message: Version string portion was too short or too long
				parsedVersion = new Version("0.0");
			}

			return parsedVersion;
        }

#if NETFRAMEWORK
		/// <summary>
		/// Returns a list of policy import extensions in the importer parameter and adds a SecurityBindingElementImporter if not already present in the list.
		/// </summary>
		/// <param name="importer">The WsdlImporter object</param>
		/// <returns>The list of PolicyImportExtension objects</returns>
		private static List<IPolicyImportExtension> AddSecurityBindingToPolicyImporter(WsdlImporter importer)
		{
			List<IPolicyImportExtension> newExts = new List<IPolicyImportExtension>();


			KeyedByTypeCollection<IPolicyImportExtension> policyExtensions = importer.PolicyImportExtensions;
			SecurityBindingElementImporter securityBindingElementImporter = policyExtensions.Find<SecurityBindingElementImporter>();

			if (securityBindingElementImporter != null)
			{
				policyExtensions.Remove<SecurityBindingElementImporter>();
			}
			else
			{
				securityBindingElementImporter = new SecurityBindingElementImporter();
			}

			newExts.Add(new AuthenticationPolicyImporter(securityBindingElementImporter));
			newExts.AddRange(policyExtensions);

			return newExts;
		}
#endif

#if NETFRAMEWORK
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Need to catch any exception here and fail.")]
		private static bool TryRetrieveMetadata(MetadataExchangeClient mcli, Uri serviceEndpoint, ServiceEndpointMetadata serviceEndpointMetadata)
		{

			bool rethrow = true;
			try
			{
				serviceEndpointMetadata.ServiceMetadata = mcli.GetMetadata(serviceEndpoint, MetadataExchangeClientMode.HttpGet);
				serviceEndpointMetadata.ServiceUrls.GeneratedFromAlternate = true;
				rethrow = false;
			}
			catch
			{
				// We don't care what the exception was at this point.  Just let the original be re-thrown.
			}

			return rethrow;
		}
#endif

        private static XmlQualifiedName GetPortTypeQName(ContractDescription contract)
		{
			return new XmlQualifiedName(contract.Name, contract.Namespace);
		}

		private static Collection<ContractDescription> CreateContractCollection(Type contract)
		{
			return new Collection<ContractDescription> { ContractDescription.GetContract(contract) };
        }

#if NETFRAMEWORK
		private static MetadataExchangeClient CreateMetadataClient(string scheme)
		{
			WSHttpBinding mexBinding = null;

			if (string.Compare(scheme, "https", StringComparison.OrdinalIgnoreCase) == 0)
			{
				mexBinding = MetadataExchangeBindings.CreateMexHttpsBinding() as WSHttpBinding;
			}
			else
			{
				mexBinding = MetadataExchangeBindings.CreateMexHttpBinding() as WSHttpBinding;
			}

			mexBinding.MaxReceivedMessageSize = int.MaxValue;

			// Set the maximum characters allowed in a table name greater than the default 16384
			mexBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;
			var mcli = new MetadataExchangeClient(mexBinding);

			mcli.ResolveMetadataReferences = true;
			mcli.MaximumResolvedReferences = 100;

			return mcli;
		}
#endif

        public static void ReplaceEndpointAddress(ServiceEndpoint endpoint, Uri adddress)
		{
			var addressBuilder = new EndpointAddressBuilder(endpoint.Address);
			addressBuilder.Uri = adddress;
			endpoint.Address = addressBuilder.ToEndpointAddress();
		}

		internal static void AdjustUserNameForWindows(ClientCredentials clientCredentials)
		{
			ClientExceptionHelper.ThrowIfNull(clientCredentials, "clientCredentials");

			if (string.IsNullOrWhiteSpace(clientCredentials.UserName.UserName))
			{
				return;
			}

			NetworkCredential credential = null;
			if (clientCredentials.UserName.UserName.IndexOf('@') > -1)
			{
				var userCreds = clientCredentials.UserName.UserName.Split('@');
				if (userCreds.Length > 1)
				{
					credential = new NetworkCredential(userCreds[0], clientCredentials.UserName.Password,
													   userCreds[1]);
				}
				else
				{
					credential = new NetworkCredential(userCreds[0], clientCredentials.UserName.Password);
				}
			}
			else if (clientCredentials.UserName.UserName.IndexOf('\\') > -1)
			{
				var userCreds = clientCredentials.UserName.UserName.Split('\\');
				if (userCreds.Length > 1)
				{
					credential = new NetworkCredential(userCreds[1], clientCredentials.UserName.Password,
													   userCreds[0]);
				}
				else
				{
					credential = new NetworkCredential(userCreds[0], clientCredentials.UserName.Password);
				}
			}
			else
			{
				credential = new NetworkCredential(clientCredentials.UserName.UserName, clientCredentials.UserName.Password);
			}

			clientCredentials.Windows.ClientCredential = credential;
			clientCredentials.UserName.UserName = string.Empty;
			clientCredentials.UserName.Password = string.Empty;
		}

		private static string GetMexDocument(Uri wsdlUri)
		{
			string baseMetaDoc = "<Metadata xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:wsx=\"http://schemas.xmlsoap.org/ws/2004/09/mex\" xmlns=\"http://schemas.xmlsoap.org/ws/2004/09/mex\">{0}</Metadata>";

			StringBuilder sbMetadataBody = new StringBuilder(); 

			string intialRequest = GetMexBodyDocument(wsdlUri);
			sbMetadataBody = ProcessMexBody(intialRequest, sbMetadataBody);
			string resultingBody = string.Format(baseMetaDoc, sbMetadataBody.ToString());
			
			sbMetadataBody.Clear();
			// turn into Xml Doc
			return resultingBody;

		}

		private static StringBuilder ProcessMexBody(string payload, StringBuilder sbMetadataBody)
        {
			string metadataSection = "<wsx:MetadataSection xmlns=\"\" Dialect=\"http://schemas.xmlsoap.org/wsdl/\" Identifier=\"{0}\">{1}</wsx:MetadataSection>";

			XmlDocument doc = new XmlDocument();
			doc.LoadXml(payload);
			var nsMgr = new XmlNamespaceManager(doc.NameTable);
			nsMgr.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");

			var TargetNSForPayload = "http://schemas.microsoft.com/xrm/2011/Contracts";
			var defintionNode = doc.SelectSingleNode(@"wsdl:definitions", nsMgr);
			if (defintionNode != null)
			{
				TargetNSForPayload = defintionNode.Attributes["targetNamespace"]?.Value;
			}
			sbMetadataBody.AppendFormat(metadataSection, TargetNSForPayload, payload);
			var nodes = doc.SelectNodes(@"wsdl:definitions/wsdl:import", nsMgr);
			if (nodes.Count > 0)
			{
				foreach (XmlNode node in nodes)
				{
					string nextLink = node.Attributes["location"]?.Value;
					if (Uri.IsWellFormedUriString(nextLink, UriKind.RelativeOrAbsolute))
					{
						// Call Get the body for the next request. 
						string mexBody = GetMexBodyDocument(new Uri(nextLink));
						if (!string.IsNullOrEmpty(mexBody))
							sbMetadataBody = ProcessMexBody(mexBody, sbMetadataBody);
					}

				}
			}

			return sbMetadataBody; 

		}

		private static string GetMexBodyDocument(Uri wsdlUri)
        {
			var client = ClientServiceProviders.Instance.GetService<IHttpClientFactory>().CreateClient("DataverseHttpClientFactory");
			HttpResponseMessage response = null;
			try
			{
				response = client.GetAsync(wsdlUri).ConfigureAwait(false).GetAwaiter().GetResult();
			}
			catch (HttpRequestException ex)
			{
				var errDetails = string.Empty;
				if (ex.InnerException is WebException wex)
				{
					errDetails = $"; details: {wex.Message} ({wex.Status})";
				}
				//LogError($"Failed to get response from: {endpoint}; error: {ex.Message}{errDetails}");
				//return details;
			}
			if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.BadRequest)
			{
				// didn't find endpoint.
				//LogError($"Failed to get Authority and Resource error. Attempt to Access Endpoint {endpoint} resulted in {response.StatusCode}.");
				//return details;
			}
			string body = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

			return body;

		}

	}
}
