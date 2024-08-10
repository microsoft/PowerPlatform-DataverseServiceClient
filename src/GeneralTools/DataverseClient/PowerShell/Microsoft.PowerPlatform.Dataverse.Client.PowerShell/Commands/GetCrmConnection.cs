// Ignore Spelling: Dataverse

using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerPlatform.Dataverse.Client.PowerShell.Commands
{
    /// <summary>
    /// this will establish a CRM connection
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PowerPlatformConnection")]
    public class GetPowerPlatformConnection : CommonAuth
    {

        /// <summary>
        /// Tenant Id of the FIC
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "FIC")]
        public Guid TenantId 
        {
            get { return base.tenantId; }
            set { base.tenantId = value; }
        }

        /// <summary>
        /// Client Id of the FIC
        /// </summary>
        [Parameter(Mandatory = true, Position = 2, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "FIC")]
        public Guid ClientId
        {
            get { return base.clientId; }
            set { base.clientId = value; }
        }

        /// <summary>
        /// Service Connection Id of the FIC
        /// </summary>
        [Parameter(Mandatory = true, Position = 3, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "FIC")]
        public Guid ServiceConnectionId
        {
            get { return base.serviceConnectionId; }
            set { base.serviceConnectionId = value; }
        }

        /// <summary>
        /// OrganizationUrl of the FIC
        /// </summary>
        [Parameter(Mandatory = true, Position = 4, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "FIC")]
        public Uri? OrganizationUrl
        {
            get { return base.orgUrlToUse; }
            set { base.orgUrlToUse = value; }
        }

        /// <summary>
        /// Environment AccessToken KeyName.
        /// </summary>
        [Parameter(Mandatory = false , Position = 4, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "FIC")]
        public string AccessTokenEnvKeyName
        {
            get { return base.accessTokenEnvKeyName; }
            set { base.accessTokenEnvKeyName = value; }
        }

        /// <summary>
        /// Used to set the connection string to connect to crm. all other connection elements are ignored when this string appears. 
        /// </summary>
        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, ParameterSetName = "ConnectionStringOnly")]
        public string ConnectionString
        {
            get { return base.connectionString; }
            set { base.connectionString = value; }
        }

        /// <summary>
        /// PreInitializion 
        /// </summary>
        protected override void BeginProcessing()
        {
            base.queryforOrgs = false;

            //string thisAssemblyPath = string.Empty;
            //var AsmList = AppDomain.CurrentDomain.GetAssemblies();
            //bool found = false;
            //// Look in the assemblies for the resources file. 
            //foreach (Assembly asm in AsmList)
            //{
            //    if (asm.FullName.ToLower().Contains(XamlResourceName))
            //    {
            //        found = true;
            //        break;
            //    }
            //}

            //if (System.IO.File.Exists(Assembly.GetExecutingAssembly().Location))
            //    thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            //if (!found)
            //{
            //    // Get the Direction Info object 
            //    System.IO.DirectoryInfo d = new System.IO.DirectoryInfo(thisAssemblyPath);
            //    // Remove file name
            //    thisAssemblyPath = System.IO.Path.GetDirectoryName(thisAssemblyPath);

            //    // Load the assembly. 
            //    if (System.IO.File.Exists(System.IO.Path.Combine(thisAssemblyPath, string.Format("{0}.dll", XamlResourceName))))
            //        Assembly.LoadFile(System.IO.Path.Combine(thisAssemblyPath, string.Format("{0}.dll", XamlResourceName)));
            //}

            base.BeginProcessing();
        }

        /// <summary>
        /// ProcessRecord method.
        /// </summary>
        protected override void ProcessRecord()
        {
            try
            {
                base.SetDiagnosticsMode();
                base.ExecuteAuth();
                if (serviceClient != null)
                    WriteObject(serviceClient);
                base.CleanUpDiagnosticsMode();
            }
            catch (Exception generalEx)
            {
                // General error write for something we don't understand going wrong. 
                WriteError(new ErrorRecord(generalEx, "-9", ErrorCategory.SyntaxError, null));
            }
        }

    }//End Class
}//End namespace
