using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
{
    /// <summary>
    /// This class will be used to support hooking into ADAL 3.x+ Call back logic.
    /// </summary>
    internal static class ADALLoggerCallBack
    {
        private static CdsTraceLogger _logEntry;

        /// <summary>
        /// Enabled PII logging for this connection.
        /// if this flag is set, it will override the value from app config.
        /// </summary>
        public static bool? EnabledPIILogging { get; set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="containsPii"></param>
        static public void Log(LogLevel level, string message, bool containsPii)
        {
            if (_logEntry == null)
                _logEntry = new CdsTraceLogger("Microsoft.IdentityModel.Clients.ActiveDirectory"); // set up logging client. 

            if (!EnabledPIILogging.HasValue)
            {
                EnabledPIILogging = true;//Utils.AppSettingsHelper.GetAppSetting("LogADALPII", false);
                _logEntry.Log($"Setting ADAL PII Logging Feature to {EnabledPIILogging.Value}", System.Diagnostics.TraceEventType.Information);
            }

            if (containsPii && !EnabledPIILogging.Value)
            {
                _logEntry.Log($"ADAL LOG EVENT SKIPPED --> PII Logging Disabled.", System.Diagnostics.TraceEventType.Warning);
                return;
            }

            // Add (PII) prefix to messages that have PII in them per AAD Message alert. 
            message = containsPii ? $"(PII){message}" : message;

            switch (level)
            {
                case LogLevel.Info:
                        _logEntry.Log(message , System.Diagnostics.TraceEventType.Information);
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
