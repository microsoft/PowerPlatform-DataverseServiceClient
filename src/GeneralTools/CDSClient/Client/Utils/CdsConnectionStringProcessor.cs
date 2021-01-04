using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Description;
using Microsoft.PowerPlatform.Cds.Client.Model;
using Microsoft.PowerPlatform.Cds.Client.Auth;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Stores Parsed connection info from the use of a CDS connection string.
	/// This is only populated when the CDS Connection string object is used, this is read only.
	/// </summary>
	internal class CdsConnectionStringProcessor
	{
		/// <summary>
		/// Sample / stand-in appID used when replacing O365 Auth
		/// </summary>
		internal static string sampleClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
		/// <summary>
		/// Sample / stand-in redirect URI used when replacing o365 Auth
		/// </summary>
		internal static string sampleRedirectUrl = "app://58145B91-0C36-4500-8554-080854F2AC97";

		/// <summary>
		/// URL of the Service being connected too.
		/// </summary>
		public Uri ServiceUri
		{
			get;
			internal set;
		}

		/// <summary>
		/// Authentication Type being used for this connection
		/// </summary>
		public AuthenticationType AuthenticationType
		{
			get;
			internal set;
		}

		/// <summary>
		/// OAuth Prompt behavior.
		/// </summary>
		public PromptBehavior PromptBehavior
		{
			get;
			internal set;
		}

		/// <summary>
		/// Claims based Delegated Authentication Url.
		/// </summary>
		public Uri HomeRealmUri
		{
			get;
			internal set;
		}

		/// <summary>
		/// Client credentials parsed from connection string
		/// </summary>
		public ClientCredentials ClientCredentials
		{
			get;
			internal set;
		}

		///// <summary>
		///// OAuth User Identifier
		///// </summary>
		//public UserIdentifier UserIdentifier
		//{
		//	get;
		//	internal set;
		//}

		/// <summary>
		/// Domain of User
		/// Active Directory Auth only.
		/// </summary>
		public string DomainName
		{
			get;
			internal set;
		}

		/// <summary>
		/// User ID of the User connection to CDS
		/// </summary>
		public string UserId
		{
			get;
			internal set;
		}

		/// <summary>
		/// Password of user, parsed from connection string
		/// </summary>
		internal string Password
		{
			get;
			set;
		}

		/// <summary>
		/// Certificate Store Name
		/// </summary>
		internal string CertStoreName { get; set; }

		/// <summary>
		/// Cert Thumbprint ID
		/// </summary>
		internal string CertThumbprint { get; set; }

		/// <summary>
		/// if set to true, then the org URI should be used directly.
		/// </summary>
		internal bool SkipDiscovery { get; set; }

		/// <summary>
		/// Client ID used in the connection string
		/// </summary>
		public string ClientId
		{
			get;
			internal set;
		}

		/// <summary>
		/// Client Secret passed from the connection string
		/// </summary>
		public string ClientSecret
		{
			get;
			internal set;
		}

		/// <summary>
		/// Organization Name parsed from the connection string.
		/// </summary>
		public string Organization
		{
			get;
			internal set;
		}

		/// <summary>
		/// Set if the connection string is for an onPremise connection
		/// </summary>
		public bool IsOnPremOauth
		{
			get;
			internal set;
		}

		/// <summary>
		/// CDS region determined by the connection string
		/// </summary>
		public string CdsGeo
		{
			get;
			internal set;
		}

		/// <summary>
		/// OAuth Redirect URI
		/// </summary>
		public Uri RedirectUri
		{
			get;
			internal set;
		}

		/// <summary>
		/// OAuth Token Store Path
		/// </summary>
		public string TokenCacheStorePath
		{
			get;
			internal set;
		}

		/// <summary>
		/// When true, specifies a unique instance of the connection should be created.
		/// </summary>
		public bool UseUniqueConnectionInstance { get; internal set; }

		/// <summary>
		/// When set to true and oAuth Mode ( not Cert ) attempts to run the login using the current user identity.
		/// </summary>
		public bool UseCurrentUser { get; set; }

		public CdsConnectionStringProcessor()
		{
		}

		private CdsConnectionStringProcessor(IDictionary<string, string> connection)
			: this(
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.ServiceUri),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.UserName),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.Password),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.Domain),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.HomeRealmUri),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.AuthType),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.RequireNewInstance),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.ClientId),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.RedirectUri),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.TokenCacheStorePath),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.LoginPrompt),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.CertStoreName),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.CertThumbprint),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.SkipDiscovery),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.IntegratedSecurity),
					connection.FirstNotNullOrEmpty(CdsConnectionStringConstants.ClientSecret)
				  )
		{
		}
		private CdsConnectionStringProcessor(string serviceUri, string userName, string password, string domain, string homeRealmUri, string authType, string requireNewInstance, string clientId, string redirectUri,
			string tokenCacheStorePath, string loginPrompt, string certStoreName, string certThumbprint, string skipDiscovery, string IntegratedSecurity , string clientSecret)
		{
			CdsTraceLogger logEntry = new CdsTraceLogger();
			Uri _serviceuriName, _realmUri;

			bool tempbool = false;
			if (bool.TryParse(skipDiscovery, out tempbool))
				SkipDiscovery = tempbool;
			else
				SkipDiscovery = true;  // changed to change defaulting behavior of skip discovery. 


			ServiceUri = GetValidUri(serviceUri, out _serviceuriName) ? _serviceuriName : null;
			HomeRealmUri = GetValidUri(homeRealmUri, out _realmUri) ? _realmUri : null;
			DomainName = !string.IsNullOrWhiteSpace(domain) ? domain : string.Empty;
			UserId = !string.IsNullOrWhiteSpace(userName) ? userName : string.Empty;
			Password = !string.IsNullOrWhiteSpace(password) ? password : string.Empty;
			ClientId = !string.IsNullOrWhiteSpace(clientId) ? clientId : string.Empty;
			ClientSecret = !string.IsNullOrWhiteSpace(clientSecret) ? clientSecret : string.Empty;
			TokenCacheStorePath = !string.IsNullOrWhiteSpace(tokenCacheStorePath) ? tokenCacheStorePath : string.Empty;
			RedirectUri = ((Uri.IsWellFormedUriString(redirectUri, UriKind.RelativeOrAbsolute)) ? new Uri(redirectUri) : null);
			CertStoreName = certStoreName;
			CertThumbprint = certThumbprint;

			// Check to see if use current user is configured. 
			bool _IntegratedSecurity = false;
			if (!string.IsNullOrEmpty(IntegratedSecurity))
				bool.TryParse(IntegratedSecurity, out _IntegratedSecurity);

			bool useUniqueConnection = true;  // Set default to true to follow the old behavior. 
			if (!string.IsNullOrEmpty(requireNewInstance))
				bool.TryParse(requireNewInstance, out useUniqueConnection);
			UseUniqueConnectionInstance = useUniqueConnection;

			//UserIdentifier = !string.IsNullOrWhiteSpace(UserId) ? new UserIdentifier(UserId, UserIdentifierType.OptionalDisplayableId) : null;

			AuthenticationType authenticationType;
			if (Enum.TryParse(authType, out authenticationType))
			{
				AuthenticationType = authenticationType;
			}
			else
			{
				AuthenticationType = AuthenticationType.InvalidConnection;
			}

			PromptBehavior loginBehavior;
			if (Enum.TryParse(loginPrompt, out loginBehavior))
			{
				PromptBehavior = loginBehavior;
			}
			else
			{
				PromptBehavior = PromptBehavior.Auto;
			}

			if (ServiceUri != null)
			{
				SetOrgnameAndOnlineRegion(ServiceUri);
			}

			//if the client Id was not passed, use Sample AppID
			if (string.IsNullOrWhiteSpace(ClientId))
			{
				logEntry.Log($"Client ID not supplied, using SDK Sample Client ID for this connection", System.Diagnostics.TraceEventType.Warning);
				ClientId = sampleClientId;// sample client ID
				if (RedirectUri == null)
					RedirectUri = new Uri(sampleRedirectUrl); // Sample app Redirect URI
			}

			if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
			{
				ClientCredentials clientCredentials = new ClientCredentials();
				clientCredentials.UserName.UserName = userName;
				clientCredentials.UserName.Password = password;
				ClientCredentials = clientCredentials;

			}

			logEntry.Dispose();

		}

		private bool GetValidUri(string uriSource, out Uri validUriResult)
		{

			bool validuri = Uri.TryCreate(uriSource, UriKind.Absolute, out validUriResult) &&
					 (validUriResult.Scheme == Uri.UriSchemeHttp || validUriResult.Scheme == Uri.UriSchemeHttps);


			return validuri;
		}
		/// <summary>
		/// Get the organization name and online region from the org
		/// </summary>
		/// <param name="serviceUri"></param>
		private void SetOrgnameAndOnlineRegion(Uri serviceUri)
		{
			// uses publicaly exposed connection parser to parse 
			string orgRegion = string.Empty;
			string orgName = string.Empty;
			bool isOnPrem = false;
			Utilities.GetOrgnameAndOnlineRegionFromServiceUri(serviceUri, out orgRegion, out orgName, out isOnPrem);
			CdsGeo = orgRegion;
			Organization = orgName;
			IsOnPremOauth = isOnPrem;
		}


		/// <summary>
		/// Parse the connection sting
		/// </summary>
		/// <param name="connectionString"></param>
		/// <returns></returns>
		public static CdsConnectionStringProcessor Parse(string connectionString )
		{
			return new CdsConnectionStringProcessor(connectionString.ToDictionary());
		}

	}

	/// <summary>
	/// Extension
	/// </summary>
	public static class Extension
	{
		/// <summary>
		/// Enum extension
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumName"></param>
		/// <returns>Enum Value</returns>
		public static T ToEnum<T>(this string enumName)
		{
			return (T)((object)Enum.Parse(typeof(T), enumName));
		}
		/// <summary>
		/// Converts a int to a Enum of the requested type (T)
		/// </summary>
		/// <typeparam name="T">Enum Type to translate too</typeparam>
		/// <param name="enumValue">Int Value too translate.</param>
		/// <returns>Enum of Type T</returns>
		public static T ToEnum<T>(this int enumValue)
		{
			return enumValue.ToString().ToEnum<T>();
		}
		/// <summary>
		/// Converts a ; separated string into a dictionary
		/// </summary>
		/// <param name="connectionString">String to parse</param>
		/// <returns>Dictionary of properties from the connection string</returns>
		public static IDictionary<string, string> ToDictionary(this string connectionString)
		{
			try
			{
				DbConnectionStringBuilder source = new DbConnectionStringBuilder
				{
					ConnectionString = connectionString
				};

				Dictionary<string, string> dictionary = source.Cast<KeyValuePair<string, object>>().
					ToDictionary((KeyValuePair<string, object> pair) => pair.Key,
					(KeyValuePair<string, object> pair) => pair.Value != null ? pair.Value.ToString() : string.Empty);
				return new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
			}
			catch
			{
				//ignore
			}
			return new Dictionary<string, string>();

		}
		/// <summary>
		/// Extension to support formating a string
		/// </summary>
		/// <param name="format">Formatting pattern</param>
		/// <param name="args">Argument collection</param>
		/// <returns>Formated String</returns>
		public static string FormatWith(this string format, params object[] args)
		{
			return format.FormatWith(CultureInfo.InvariantCulture, args);
		}
		/// <summary>
		/// Extension to get the first item in a dictionary if the dictionary contains the key.
		/// </summary>
		/// <typeparam name="TKey">Type to return</typeparam>
		/// <param name="dictionary">Dictionary to search</param>
		/// <param name="keys">Collection of Keys to find.</param>
		/// <returns></returns>
		public static string FirstNotNullOrEmpty<TKey>(this IDictionary<TKey, string> dictionary, params TKey[] keys)
		{
			return (
				from key in keys
				where dictionary.ContainsKey(key) && !string.IsNullOrEmpty(dictionary[key])
				select dictionary[key]).FirstOrDefault<string>();
		}

	}
}
