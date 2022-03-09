using Microsoft.Identity.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    /// <summary>
    /// This class will be used to support hooking into MSAL Call back logic.
    /// </summary>
    internal static class MSALLoggerCallBack
    {
        private static DataverseTraceLogger _logEntry;

        /// <summary>
        /// Enabled PII logging for this connection.
        /// if this flag is set, it will override the value from app config.
        /// </summary>
        public static bool? EnabledPIILogging { get; set; } = null;

        /// <summary>
        ///
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="containsPii"></param>
        static public void Log(LogLevel level, string message, bool containsPii)
        {
            if (_logEntry == null)
                _logEntry = new DataverseTraceLogger(typeof(LogCallback).Assembly.GetName().Name); // set up logging client.

            if (!EnabledPIILogging.HasValue)
            {
                EnabledPIILogging = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>().Value.MSALEnabledLogPII;
                _logEntry.Log($"Setting MSAL PII Logging Feature to {EnabledPIILogging.Value}", System.Diagnostics.TraceEventType.Information);
            }

            if (containsPii && !EnabledPIILogging.Value)
            {
                return;
            }

            // Add (PII) prefix to messages that have PII in them per AAD Message alert.
            message = containsPii ? $"(PII){message}" : message;

            switch (level)
            {
                case LogLevel.Info:
                    _logEntry.Log(message, System.Diagnostics.TraceEventType.Information);
                    break;
                case LogLevel.Verbose:
                    _logEntry.Log(message, System.Diagnostics.TraceEventType.Verbose);
                    break;
                case LogLevel.Warning:
                    _logEntry.Log(message, System.Diagnostics.TraceEventType.Warning);
                    break;
                case LogLevel.Error:
                    _logEntry.Log(message, System.Diagnostics.TraceEventType.Error);
                    break;
                default:
                    break;
            }
        }

    }
}
