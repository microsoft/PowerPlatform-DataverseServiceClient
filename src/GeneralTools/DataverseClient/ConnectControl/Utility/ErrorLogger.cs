using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
    /// <summary>
    /// Used to log errors to disk as part of connection logic for debugging or reporting an error concerning connecting to CRM   
    /// </summary>
    public class ErrorLogger
    {
        /// <summary>
        /// If Set, the directory requested here is used to set the 
        /// </summary>
        public static string LogfileDirectoryOverride { get; set; }

        /// <summary>
        /// Name of the Connection Error Log file.
        /// </summary>
        private const string ErrorFileName = "Login_ErrorLog.log";

        /// <summary>
        /// Login Tracing System
        /// </summary>
        private static LoginTracer tracer = new LoginTracer();

        private static FileVersionInfo fileVersionInfo;
        /// <summary>
        /// Writes a Exception to the log file.
        /// </summary>
        /// <param name="objException"></param>
        public static void WriteToFile(Exception objException)
        {

            tracer.Log("Exception logged by the CRM Connector control:", System.Diagnostics.TraceEventType.Error, objException);
            try
            {
                //Handle managed and unmanaged code using this connector tool.
                fileVersionInfo = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().MainModule.FileName);

                string baseDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileVersionInfo.CompanyName, fileVersionInfo.ProductName, fileVersionInfo.ProductVersion);
                if (!Directory.Exists(baseDir))
                {
                    Directory.CreateDirectory(baseDir);
                }
                // search the file below the current directory
                string retFilePath = Path.Combine(baseDir, ErrorFileName);
                WriteErrorLog(retFilePath, objException);
            }
            catch (Exception ex)
            {
                tracer.Log("Failed to Log Error to disk", System.Diagnostics.TraceEventType.Error, ex);
            }

        }

        /// <summary>
        /// Loads the log file in Notepad
        /// </summary> 
        public static void LaunchLogFile()
        {
            using (System.Diagnostics.Process p = new System.Diagnostics.Process())
            {
                p.StartInfo.ErrorDialog = true;


                // If logfileoverride is not set then get the current assembly execution path else set override directory.
                string workingDir = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), fileVersionInfo.CompanyName, fileVersionInfo.ProductName, fileVersionInfo.ProductVersion);
                p.StartInfo.WorkingDirectory = workingDir;
                p.StartInfo.FileName = "Notepad.exe";
                p.StartInfo.Arguments = ErrorFileName;
                p.Start();
            }

        }

        /// <summary>
        /// Writes an error log entry to the log file
        /// </summary>
        /// <param name="strPathName">Fully qualified name of the log file</param>
        /// <param name="objException">Exception to be written</param>
        /// <returns>true of success, false on failure</returns>
        private static bool WriteErrorLog(string strPathName, Exception objException)
        {

            bool bReturn = false;

            string strException = string.Empty;
            try
            {
                using (StreamWriter sw = new StreamWriter(strPathName, true))
                {
                    LogExceptionToFile(objException, sw, 0);

                    sw.WriteLine(string.Empty);
                    sw.Flush();
                }
                bReturn = true;
            }
            catch (Exception)
            {
                bReturn = false;
            }
            return bReturn;
        }

        /// <summary>
        /// Logs the error text to the stream
        /// </summary>
        /// <param name="objException">Exception to be written</param>
        /// <param name="sw">Stream writer to use to write the exception</param>
        /// <param name="level">level of the exception, this deals with inner exceptions</param>
        private static void LogExceptionToFile(Exception objException, StreamWriter sw, int level)
        {
            if (level != 0)
                sw.WriteLine(string.Format("Inner Exception Level {0}\t: ", level));

            sw.WriteLine("Source\t: " +
                (objException.Source != null ? objException.Source.ToString().Trim() : "Not Provided"));
            sw.WriteLine("Method\t: " +
                (objException.TargetSite != null ? objException.TargetSite.Name.ToString() : "Not Provided"));
            sw.WriteLine("Date\t: " +
                    DateTime.Now.ToShortDateString());
            sw.WriteLine("Time\t: " +
                    DateTime.Now.ToLongTimeString());
            sw.WriteLine("Error\t: " +
                (string.IsNullOrEmpty(objException.Message) ? "Not Provided" : objException.Message.ToString().Trim()));
            sw.WriteLine("Stack Trace\t: " +
                (string.IsNullOrEmpty(objException.StackTrace) ? "Not Provided" : objException.StackTrace.ToString().Trim()));
            sw.WriteLine("======================================================================================================================");

            level++;
            if (objException.InnerException != null)
                LogExceptionToFile(objException.InnerException, sw, level);

        }
    }
}
