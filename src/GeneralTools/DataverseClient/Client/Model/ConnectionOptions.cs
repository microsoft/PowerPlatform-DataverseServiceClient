using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Model
{
    /// <summary>
    /// Describes connection Options for the Dataverse ServiceClient
    /// </summary>
    public class ConnectionOptions
    {
        /// <summary>
        ///  Defines which type of login will be used to connect to Dataverse
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; } = AuthenticationType.OAuth;

        /// <summary>
        /// URL of the Dataverse Instance to connect too.
        /// </summary>
        public Uri ServiceUri { get; set; }

        /// <summary>
        /// User Name to use - Used with Interactive Login scenarios
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// User Password to use - Used with Interactive Login scenarios
        /// </summary>
        public SecureString Password { get; set; }

        /// <summary>
        /// User Domain to use - Use with Interactive Login for On Premises 
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// Home Realm to use when working with AD Federation. 
        /// </summary>
        public Uri HomeRealmUri { get; set; }

        /// <summary>
        /// Require a unique instance of the Dataverse ServiceClient per Login. 
        /// </summary>
        public bool RequireNewInstance { get; set; } = true;

        /// <summary>
        /// Client \ Application ID to be used when logging into Dataverse. 
        /// </summary>
        public string ClientId { get; set; } = DataverseConnectionStringProcessor.sampleClientId;

        /// <summary>
        /// Client Secret Id to use to login to Dataverse
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// Redirect Uri to use when connecting to dataverse.  Required for OAuth Authentication. 
        /// </summary>
        public Uri RedirectUri { get; set; } = new Uri(DataverseConnectionStringProcessor.sampleRedirectUrl);

        /// <summary>
        /// Path and FileName for MSAL Token Cache.  Used only for OAuth - User Interactive flows. 
        /// </summary>
        public string TokenCacheStorePath { get; set; }

        /// <summary>
        /// Type of Login prompt to use. 
        /// </summary>
        public PromptBehavior? LoginPrompt { get; set; }

        /// <summary>
        /// Certificate ThumbPrint to use to lookup machine certificate to use for authentication.
        /// </summary>
        public string CertificateThumbprint { get; set; }

        /// <summary>
        /// Certificate store name to look up thumbprint. <see cref="System.Security.Cryptography.X509Certificates.StoreName"/>
        /// </summary>
        public System.Security.Cryptography.X509Certificates.StoreName CertificateStoreName { get; set; }

        /// <summary>
        /// Skip discovery leg when connecting to Dataverse 
        /// </summary>
        public bool SkipDiscovery { get; set; } = true;

        /// <summary>
        /// (Windows Only) If True, Uses the current user of windows to attempt the login with
        /// </summary>
        public bool UseCurrentUserForLogin { get; set; }

        /// <summary>
        /// ILogger Interface for Dataverse ServiceClient. <see cref="Microsoft.Extensions.Logging.ILogger"/>
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Function that Dataverse ServiceClient will call to request an access token for a given connection.  
        /// </summary>
        public Func<string, Task<string>> AccessTokenProviderFunctionAsync { get; set; }

        /// <summary>
        /// Function that Dataverse ServiceClient will call to request custom headers
        /// </summary>
        public Func<Task<Dictionary<string, string>>> RequestAdditionalHeadersAsync { get; set; }
    }
}
