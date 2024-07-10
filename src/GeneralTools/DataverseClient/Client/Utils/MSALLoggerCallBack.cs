using Microsoft.Identity.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    /// <summary>
    /// This class will be used to support hooking into MSAL Call back logic.
    /// </summary>
    internal class MSALLoggerCallBack
    {

        public DataverseTraceLogger LogSink { get; set; } = null;

        /// <summary>
        /// Enabled PII logging for this connection.
        /// if this flag is set, it will override the value from app config.
        /// </summary>
        public bool? EnabledPIILogging { get; set; } = null;

        public MSALLoggerCallBack(DataverseTraceLogger logSink = null, bool? enabledPIILogging = null)
        {
            LogSink = logSink;
            EnabledPIILogging = enabledPIILogging;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <param name="containsPii"></param>
        public void Log(LogLevel level, string message, bool containsPii)
        {
            bool createdLogSource = false;
            if (LogSink == null)
            {
                createdLogSource = true;
                LogSink = new DataverseTraceLogger(typeof(LogCallback).Assembly.GetName().Name); // set up logging client.
            }

            if (!EnabledPIILogging.HasValue)
            {
                EnabledPIILogging = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>().Value.MSALEnabledLogPII;
                LogSink.Log($"Setting MSAL PII Logging Feature to {EnabledPIILogging.Value}", System.Diagnostics.TraceEventType.Information);
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
                    LogSink.Log(message, System.Diagnostics.TraceEventType.Information);
                    break;
                case LogLevel.Verbose:
                    LogSink.Log(message, System.Diagnostics.TraceEventType.Verbose);
                    break;
                case LogLevel.Warning:
                    LogSink.Log(message, System.Diagnostics.TraceEventType.Warning);
                    break;
                case LogLevel.Error:
                    LogSink.Log(message, System.Diagnostics.TraceEventType.Error);
                    break;
                default:
                    break;
            }

            if (createdLogSource)
            {
                LogSink.Dispose();
            }
        }

    }
}
