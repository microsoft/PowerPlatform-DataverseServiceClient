using System.Diagnostics;
using System.Reflection;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    internal static class Environs
    {
        private static object _initLock = new object();

        public static string FileVersion { get; private set; }

        static Environs()
        {
            if (string.IsNullOrEmpty(FileVersion))
            {
                lock (_initLock)
                {
                    if (string.IsNullOrEmpty(FileVersion))
                    {
                        FileVersion = "Unknown";
                        try
                        {
                            string location = Assembly.GetExecutingAssembly().Location;
                            if (!string.IsNullOrEmpty(location))
                            {
                                string version = FileVersionInfo.GetVersionInfo(location).FileVersion;
                                if (!string.IsNullOrEmpty(version))
                                {
                                    FileVersion = version;
                                }
                            }
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
