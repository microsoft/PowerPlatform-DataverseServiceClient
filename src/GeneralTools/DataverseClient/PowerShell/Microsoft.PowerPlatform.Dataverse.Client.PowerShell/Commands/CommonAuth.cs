// Ignore Spelling: Dataverse Auth queryfor Orgs

using System.Management.Automation;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.Client.PowerShell.Commands
{
    public class CommonAuth : BaseCmdLet 
    {
        #region Vars
        /// <summary>
        /// Connection timeout setting for CRM connection. 
        /// </summary>
        private int maxcrmconnectiontimeoutminutes = -1;
        /// <summary>
        /// Connection string to use when connecting to CRM
        /// </summary>
        internal string connectionString = string.Empty;

        /// <summary>
        /// Url of organization to connect too. 
        /// </summary>
        internal Uri? orgUrlToUse;
        /// <summary>
        /// Tenant ID for FIC
        /// </summary>
        internal Guid tenantId;
        /// <summary>
        /// FIC Client Id
        /// </summary>
        internal Guid clientId;
        /// <summary>
        /// Service Connection Id
        /// </summary>
        internal Guid serviceConnectionId;
        /// <summary>
        /// Environment AccessToken KeyName.
        /// </summary>
        internal string accessTokenEnvKeyName = string.Empty;

        /// <summary>
        /// CrmSvcConnection
        /// </summary>
        protected ServiceClient? serviceClient = null;

        ///// <summary>
        ///// Progress Record. 
        ///// </summary>
        //private ProgressRecord? connProgress = null;

        /// <summary>
        /// Error Record if any. 
        /// </summary>
        private ErrorRecord? errorRecord = null;

        /// <summary>
        /// set when querying for orgs. 
        /// </summary>
        public bool queryforOrgs = false;

        /// <summary>
        /// PowerShell MultiThreaded Adapter. 
        /// </summary>
        //private PowerShellAdapter? adapter = null;

        /// <summary>
        /// Percentage completed. 
        /// </summary>
        //private int percentCmp = 0; 


        #endregion

        #region Properties.
        
        /// <summary>
        /// The following is the definition of the input parameter "MaxConnectionTimeOutMinutes".       
        /// User credential used to login to CRM
        /// </summary>
        [Parameter(Position = 20, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public int MaxConnectionTimeOutMinutes
        {
            get { return this.maxcrmconnectiontimeoutminutes; }
            set { this.maxcrmconnectiontimeoutminutes = value; }
        }

        #endregion

        public CommonAuth()
        {
            // Auto Add TLS 1.2 support which is required. 
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        }

        /// <summary>
        /// Get Orgs. 
        /// </summary>
        public void ExecuteGetOrgs()
        {
            ExecuteAuth();
            if (errorRecord != null)
                WriteError(errorRecord); 
        }

        /// <summary>
        /// Run Auth. 
        /// </summary>
        public void ExecuteAuth()
        {
            RunAuth();
            if (!queryforOrgs && errorRecord != null)
                WriteError(errorRecord); 
        }

        /// <summary>
        /// Authenticate with Dataverse. 
        /// </summary>
        private void RunAuth()
        {
            if (serviceConnectionId != null && serviceConnectionId != Guid.Empty)
            {
                serviceClient = AzPipelineFederatedIdentityAuth.CreateServiceClient(
                    tenantId.ToString(),
                    clientId.ToString(),
                    serviceConnectionId.ToString(),
                 new ConnectionOptions()
                 {
                     ServiceUri = orgUrlToUse,
                     Logger = CreateILogger()
                 }
                );
            }
            else if (!string.IsNullOrEmpty(connectionString))
            {
                // Connection string is present. 
                ServiceClient localServiceClient = new ServiceClient(connectionString, CreateILogger());
                if (localServiceClient != null && localServiceClient.IsReady)
                    serviceClient = localServiceClient;
                else
                {
                    if (!string.IsNullOrEmpty(localServiceClient?.LastError) || localServiceClient?.LastException != null)
                    {
                        errorRecord = new ErrorRecord(new Exception(string.Format(CultureInfo.InvariantCulture, "Failed to connect to Dataverse: {0}", localServiceClient.LastError), localServiceClient.LastException), "-10", ErrorCategory.PermissionDenied, null);
                        WriteError(errorRecord);
                        serviceClient = null;
                    }
                }
            }
            else
            {
                // No connection string. 
                errorRecord = new ErrorRecord(new Exception("Connection string is required to connect to Dataverse"), "-1", ErrorCategory.InvalidArgument, null);
                WriteError(errorRecord);
                serviceClient = null;
            }

            // Set connection timeout if required. 
            if (serviceClient != null && MaxConnectionTimeOutMinutes != -1)
            {
                WriteVerbose(string.Format(CultureInfo.InstalledUICulture, "Dataverse Connection Timeout set to {0} Minutes", MaxConnectionTimeOutMinutes));
                SetConnectionTimeoutValues(new TimeSpan(0, MaxConnectionTimeOutMinutes, 0));
            }
            else
                if (serviceClient != null)
                WriteVerbose(string.Format(CultureInfo.InstalledUICulture, "Dataverse Connection Timeout is set to {0} Minutes", GetConnectionTimeoutValues().Minutes));
        }

        #region Private classes.
 
        /// <summary>
        /// Updates the timeout value to extend the amount of item that a request will wait. 
        /// </summary>
        public void SetConnectionTimeoutValues(TimeSpan TimeOutToSet)
        {
            ServiceClient.MaxConnectionTimeout = TimeOutToSet;
        }

        /// <summary>
        /// Gets the current connection time out value. 
        /// </summary>
        /// <returns></returns>
        private TimeSpan GetConnectionTimeoutValues()
        {
            return ServiceClient.MaxConnectionTimeout; 
        }


        #endregion

    }
    #region Extension Methods for SecureString
    ///// <summary>
    ///// Adds a extension to Secure string
    ///// </summary>
    //internal static class SecureStringExtensions
    //{
    //    /// <summary>
    //    /// DeCrypt a Secure password 
    //    /// </summary>
    //    /// <param name="value"></param>
    //    /// <returns></returns>
    //    public static string ToUnsecureString(this SecureString value)
    //    {
    //        if (null == value)
    //            throw new ArgumentNullException("value");

    //        // Get a pointer to the secure string memory data. 
    //        IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
    //        try
    //        {
    //            // DeCrypt
    //            return Marshal.PtrToStringUni(ptr);
    //        }
    //        finally
    //        {
    //            // release the pointer. 
    //            Marshal.ZeroFreeGlobalAllocUnicode(ptr);
    //        }
    //    }
    //}
    #endregion
}
