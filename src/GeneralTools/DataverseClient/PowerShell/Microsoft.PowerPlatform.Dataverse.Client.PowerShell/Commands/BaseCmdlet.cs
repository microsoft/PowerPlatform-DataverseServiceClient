// Ignore Spelling: Dataverse

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Management.Automation;
using System.Reflection;

namespace Microsoft.PowerPlatform.Dataverse.Client.PowerShell.Commands
{ 
    public class BaseCmdLet : PSCmdlet
    {
        #region Vars
        ///// <summary>
        ///// file Writer Link
        ///// </summary>
        //private Microsoft.Xrm.Tooling.Connector.DynamicsFileLogTraceListener commonFileWriter = null;

        /// <summary>
        /// when present and populated, this will write the logs to the directory specified.  loges are written only when -verbose is chosen. 
        /// </summary>
        [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
        public string LogWriteDirectory { get; set; } = string.Empty;

//#if DEBUG
//        private System.Diagnostics.DefaultTraceListener commonConsoleListener = null; 
//#endif

        #endregion

        internal ILogger CreateILogger()
        {
            var ConfigFileLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "appsettings.json");
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile(ConfigFileLocation, optional: true, reloadOnChange: true)
                .Build();
            
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                    builder.AddConsole(options =>
                    {
#pragma warning disable CS0618 // Type or member is obsolete
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
#pragma warning restore CS0618 // Type or member is obsolete
                    })
                    .AddConfiguration(config.GetSection("Logging")));

            return loggerFactory.CreateLogger<Microsoft.PowerPlatform.Dataverse.Client.ServiceClient>();
        }

        /// <summary>
        /// Determines if its necessary to enable tracing. 
        /// </summary>
        internal void SetDiagnosticsMode()
        {
            if (this.MyInvocation.BoundParameters.ContainsKey("verbose") && (SwitchParameter)this.MyInvocation.BoundParameters["verbose"])
            {
//                CrmConnectControl.Utility.TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Verbose;
//                Connector.TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Verbose;

//                if (CrmConnectControl.Utility.TraceControlSettings.TraceLevel != System.Diagnostics.SourceLevels.Off)
//                {
//                    // Create a common TraceFile Writer. 
//                    if (commonFileWriter == null)
//                    {
//                        if (!string.IsNullOrEmpty(LogWriteDirectory) && System.IO.Directory.Exists(LogWriteDirectory))
//                        {
//                            commonFileWriter = new Connector.DynamicsFileLogTraceListener()
//                            {
//                                BaseFileName = "Microsoft.PowerPlatform.Dataverse.Client.PowerShell",
//                                Location = VisualBasic.Logging.LogFileLocation.Custom,
//                                CustomLocation = LogWriteDirectory
//                            };
//                        }
//                        else
//                        {
//                            commonFileWriter = new Connector.DynamicsFileLogTraceListener()
//                            {
//                                BaseFileName = "Microsoft.PowerPlatform.Dataverse.Client.PowerShell",
//                                Location = VisualBasic.Logging.LogFileLocation.LocalUserApplicationDirectory
//                            };

//                        }
                        
//                        this.WriteVerbose(string.Format("Verbose output log file: '{0}'", commonFileWriter.FullLogFileName));

//                        CrmConnectControl.Utility.TraceControlSettings.AddTraceListener(commonFileWriter);
//                        Connector.TraceControlSettings.AddTraceListener(commonFileWriter);
//                    }

//#if DEBUG
//                    if ( commonConsoleListener == null )
//                    {
//                        commonConsoleListener = new System.Diagnostics.DefaultTraceListener();
//                        CrmConnectControl.Utility.TraceControlSettings.AddTraceListener(commonFileWriter);
//                        Connector.TraceControlSettings.AddTraceListener(commonFileWriter);
//                    }
//#endif 

//                }
            }
            else
                if (this.MyInvocation.BoundParameters.ContainsKey("verbose") && !(SwitchParameter)this.MyInvocation.BoundParameters["verbose"])
                {
                    //// forces it off. 
                    //CrmConnectControl.Utility.TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Off;
                    //Connector.TraceControlSettings.TraceLevel = System.Diagnostics.SourceLevels.Off;
                }
        }

        /// <summary>
        /// Cleans up open TraceWriters
        /// </summary>
        internal void CleanUpDiagnosticsMode()
        {
            //CrmConnectControl.Utility.TraceControlSettings.CloseListeners();
            //Connector.TraceControlSettings.CloseListeners();
        }

    }

    #region Threading Support Class
    /// <summary>
    /// Type of write to use. 
    /// </summary>
    internal enum WriteInfoType
    {
        Warning = 0,
        Verbose, 
        Debug
    }

    /// <summary>
    /// holder class to signal handling to the cmdlet adapter writer. 
    /// </summary>
    internal class GeneralWriteInfo
    {
        /// <summary>
        ///  Warning message to write. 
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Type of Message in this class.
        /// </summary>
        public WriteInfoType MessageType { get; set; }

        /// <summary>
        /// Warning Message to write. 
        /// </summary>
        /// <param name="warningMessage"></param>
        public GeneralWriteInfo(string warningMessage, WriteInfoType messageType)
        {
            Message = warningMessage;
            MessageType = messageType;
        }
    }

    #endregion
}
