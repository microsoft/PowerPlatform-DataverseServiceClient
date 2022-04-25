using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Xrm.Sdk.Organization;
using System.Diagnostics;

namespace Microsoft.PowerPlatform.Dataverse.Client.Auth.TokenCache
{
    internal class FileBackedTokenCacheHints
    {
        public string cacheFileName { get; set; }
        public string cacheFileDirectory { get; set; }

        // Linux KeyRing
        public string linuxSchemaName { get; set; }
        public string linuxCollection { get; set; }
        public string linuxLabel { get; set; }
        public KeyValuePair<string, string> linuxAttr1 { get; set; }
        public KeyValuePair<string, string> linuxAttr2 { get; set; }

        // MAC KeyRing
        public string macKeyChainServiceName { get; set; }
        public string macKeyChainServiceAccount { get; set; }

        /// <summary>
        /// Setup File Backed Token Storage with token path.
        /// </summary>
        /// <param name="tokenPathAndFileName"></param>
        public FileBackedTokenCacheHints(string tokenPathAndFileName)
        {
            string hostName = "DvBaseClient";
            if (AppDomain.CurrentDomain != null)
            {
                hostName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
            }
            string hostVersion = Environs.XrmSdkFileVersion;
            string companyName = typeof(OrganizationDetail).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;


            if (string.IsNullOrEmpty(tokenPathAndFileName))
            {
                tokenPathAndFileName = Path.Combine(MsalCacheHelper.UserRootDirectory, companyName?.Replace(" ", "_"), hostName, hostVersion, "dvtokens.dat");
            }

            System.Diagnostics.Trace.WriteLine($"TokenCacheFilePath: {tokenPathAndFileName}");

            cacheFileDirectory = Path.GetDirectoryName(tokenPathAndFileName);
            cacheFileName = Path.GetFileName(tokenPathAndFileName);

            // configure MAC properties:
            macKeyChainServiceName = $"{hostName}_service";
            macKeyChainServiceAccount = $"{hostName}_account";

            // configure LinuxKeys
            linuxSchemaName = $"{hostName}.dvserviceclient.tokencache";
            linuxCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
            linuxLabel = $"Token Storage for {hostName}";
            linuxAttr1 = new KeyValuePair<string, string>("Version", string.IsNullOrEmpty(hostVersion) ? "1.0" : hostVersion);
            linuxAttr2 = new KeyValuePair<string, string>("ProductGroup", hostName);
        }
    }
}
