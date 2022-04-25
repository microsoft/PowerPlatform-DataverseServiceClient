using Microsoft.Xrm.Sdk.Organization;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    internal static class Environs
    {
        private static object _initLock = new object();

        /// <summary>
        /// Version number of the XrmSDK 
        /// </summary>
        public static string XrmSdkFileVersion { get; private set; }

        public static string DvSvcClientFileVersion { get; private set; }

        static Environs()
        {

            if (string.IsNullOrEmpty(XrmSdkFileVersion))
            {
                lock(_initLock)
                {
                    if ( string.IsNullOrEmpty(XrmSdkFileVersion))
                    {
                        XrmSdkFileVersion = "Unknown";
                        try
                        {
                            XrmSdkFileVersion = typeof(OrganizationDetail).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version ?? FileVersionInfo.GetVersionInfo(typeof(OrganizationDetail).Assembly.Location).FileVersion;
                        }
                        catch { }
                    }
                }
            }


            if (string.IsNullOrEmpty(DvSvcClientFileVersion))
            {
                lock (_initLock)
                {
                    if (string.IsNullOrEmpty(DvSvcClientFileVersion))
                    {
                        DvSvcClientFileVersion = "Unknown";
                        try
                        {
                            DvSvcClientFileVersion = typeof(ServiceClient).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version ?? FileVersionInfo.GetVersionInfo(typeof(ServiceClient).Assembly.Location).FileVersion;
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }
    }
}
