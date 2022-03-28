using System.Diagnostics;
using System.Reflection;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    internal static class Environs
    {
        public static string FileVersion => Utilities.GetAssemblyFileVersion(Assembly.GetExecutingAssembly());
    }
}
