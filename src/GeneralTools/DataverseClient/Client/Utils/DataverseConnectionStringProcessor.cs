using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Linq;
using System.Reflection;
using System.ServiceModel.Description;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Stores Parsed connection info from the use of a CDS connection string.
    /// This is only populated when the CDS Connection string object is used, this is read only.
    /// </summary>
    internal class DataverseConnectionStringProcessor
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
        public string Geo
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

        public DataverseConnectionStringProcessor()
        {
        }

        private DataverseConnectionStringProcessor(IDictionary<string, string> connection, ILogger logger)
            : this(
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.ServiceUri),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.UserName),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.Password),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.Domain),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.HomeRealmUri),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.AuthType),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.RequireNewInstance),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.ClientId),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.RedirectUri),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.TokenCacheStorePath),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.LoginPrompt),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.CertStoreName),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.CertThumbprint),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.SkipDiscovery),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.IntegratedSecurity),
                    connection.FirstNotNullOrEmpty(ConnectionStringConstants.ClientSecret),
                    logger
                  )
        {
        }
        private DataverseConnectionStringProcessor(string serviceUri, string userName, string password, string domain, string homeRealmUri, string authType, string requireNewInstance, string clientId, string redirectUri,
            string tokenCacheStorePath, string loginPrompt, string certStoreName, string certThumbprint, string skipDiscovery, string IntegratedSecurity, string clientSecret, ILogger logger)
        {
            DataverseTraceLogger logEntry = new DataverseTraceLogger(logger);
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
            TokenCacheStorePath = !string.IsNullOrWhiteSpace(tokenCacheStorePath) ? tokenCacheStorePath : null;
            RedirectUri = ((Uri.IsWellFormedUriString(redirectUri, UriKind.RelativeOrAbsolute)) ? new Uri(redirectUri) : null);
            CertStoreName = certStoreName;
            CertThumbprint = certThumbprint;

            // Check to see if use current user is configured.
            bool _IntegratedSecurity = false;
            if (!string.IsNullOrEmpty(IntegratedSecurity))
                bool.TryParse(IntegratedSecurity, out _IntegratedSecurity);
            UseCurrentUser = _IntegratedSecurity;

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
                logEntry?.Log($"Authentication Type \"{authType}\" is not a valid Authentication Type.", System.Diagnostics.TraceEventType.Error);
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
            if (authenticationType != AuthenticationType.AD && authenticationType != AuthenticationType.ExternalTokenManagement
                && string.IsNullOrWhiteSpace(ClientId))
            {
                logEntry.Log($"Client ID not supplied, using SDK Sample Client ID for this connection", System.Diagnostics.TraceEventType.Warning);
                ClientId = sampleClientId;// sample client ID
                if (RedirectUri == null)
                    RedirectUri = new Uri(sampleRedirectUrl); // Sample app Redirect URI
            }

            if (!string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(password))
            {
                ClientCredentials clientCredentials = new ClientCredentials();
                if (AuthenticationType == AuthenticationType.AD && !string.IsNullOrWhiteSpace(domain))
                {
                    clientCredentials.Windows.ClientCredential = new NetworkCredential(userName, password, domain);
                }
                else
                {
                    clientCredentials.UserName.UserName = userName;
                    clientCredentials.UserName.Password = password;
                }
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
            Geo = orgRegion;
            Organization = orgName;
            IsOnPremOauth = isOnPrem;
        }


        /// <summary>
        /// Parse the connection sting
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="logger">Logging provider <see cref="ILogger"/></param>
        /// <returns></returns>
        public static DataverseConnectionStringProcessor Parse(string connectionString, ILogger logger = null)
        {
            return new DataverseConnectionStringProcessor(connectionString.ToDictionary(), logger);
        }

    }
}
