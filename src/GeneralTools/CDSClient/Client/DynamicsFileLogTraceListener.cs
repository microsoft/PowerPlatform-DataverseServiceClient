using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
#if NET462
using Microsoft.VisualBasic.Logging;
#endif
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.PowerPlatform.Cds.Client
{
#if NET462 // Only available in 4.6.2 right now. 
    /// <summary>
    /// Extension to the FileLogTraceListner class.
    /// </summary>
    public class DynamicsFileLogTraceListener : FileLogTraceListener
    {
        private static bool check = false;
        private string defBaseFileName = string.Empty;
        private Queue<string> logfiles;
        private bool _isInitialized = false;
        private int maxFileCount = -1;

        private string logFileName
        {
            get
            {
                string _filename;
                if (defBaseFileName == string.Empty)
                {
                    defBaseFileName = this.BaseFileName;
                }
                _filename = defBaseFileName + "_" + DateTime.Now.ToString("hhmmssfff");
                return _filename;
            }
        }

        /// <summary>
        /// Number of files to keep while rolling.
        /// </summary>
        public int MaxFileCount
        {
            get
            {
                //already configured, return
                if (maxFileCount != -1) return maxFileCount;

                //MaxFileCount not specified, return -1
                if (!Attributes.ContainsKey("MaxFileCount")) return maxFileCount;
                int _maxFileCountLocal = -1;
                //if parsable, else return -1
                if (int.TryParse(Attributes["MaxFileCount"], out _maxFileCountLocal))
                {
                    maxFileCount = _maxFileCountLocal;
                    // if MaxFileCount is invalid then return -1;
                    if (_maxFileCountLocal <= 0)
                    {
                        maxFileCount = -1;
                    }
                }
                return maxFileCount;
            }
        }


        /// <summary>
        /// The class constructor.
        /// </summary>
        public DynamicsFileLogTraceListener()
            : base()
        {
            this.LogFileCreationSchedule = LogFileCreationScheduleOption.Daily;
        }

        /// <summary>
        /// The class constructor.
        /// </summary>
        /// <param name="name">Source Path</param>
        public DynamicsFileLogTraceListener(string name)
            : base(name)
        {

        }

        private void Initialize()
        {
            if (_isInitialized) return;

            //if maxfilecount = -1, default to legacy
            if (MaxFileCount == -1) return;

            this.DiskSpaceExhaustedBehavior = DiskSpaceExhaustedOption.ThrowException;
            this.BaseFileName = logFileName;
            string dir = Path.GetDirectoryName(this.FullLogFileName);
            if (dir != null)
            {
                IEnumerable<string> files = new DirectoryInfo(dir).GetFiles()
                    .Where(s => s.Name.StartsWith(defBaseFileName + "_", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => f.FullName)
                    .ToList();
                logfiles = new Queue<string>(files);
            }
            else
            {
                logfiles = new Queue<string>();
            }
            _isInitialized = true;
        }

        /// <summary>
        /// Checks if the CustomLocation Path has write permission
        /// </summary>
        /// <param name="Location">Custom Location Path</param>
        /// <returns>boolean</returns>
        protected static bool IsWritePermitted(string Location)
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(Location);
                DirectorySecurity dirSecurity = dirInfo.GetAccessControl();
                AuthorizationRuleCollection rules = dirSecurity.GetAccessRules(true, true, typeof(NTAccount));
                WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(currentUser);
                foreach (AuthorizationRule rule in rules)
                {
                    FileSystemAccessRule fsAccessRule = rule as FileSystemAccessRule;
                    if (fsAccessRule == null)
                        continue;
                    if ((fsAccessRule.FileSystemRights & FileSystemRights.WriteData) > 0)
                    {
                        NTAccount ntAccount = rule.IdentityReference as NTAccount;
                        if (ntAccount == null)
                        {
                            continue;
                        }
                        if (principal.IsInRole(ntAccount.Value))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                EventLog.WriteEntry("application", ex.Message, EventLogEntryType.Error);
            }
            EventLog.WriteEntry("application", "Write Acess Denied to the path" + Location, EventLogEntryType.Warning);
            return false;
        }

        /// <summary>
        ///Trace event is Overriden to check the Log file Access.
        /// </summary>
        /// <param name="eventCache">TraceEventCache</param>
        /// <param name="source">Source</param>
        /// <param name="eventType">TraceEventType</param>
        /// <param name="id">Id</param>
        /// <param name="format">Format Options</param>
        /// <param name="args">Array of message objects</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            try
            {
                if (check == false && this.Location == LogFileLocation.Custom && !IsWritePermitted(this.CustomLocation))
                {
                    this.Location = LogFileLocation.LocalUserApplicationDirectory;
                    check = true;
                }
            }
            catch (ArgumentException)
            {
                this.Location = LogFileLocation.LocalUserApplicationDirectory;
                check = true;
            }
            try
            {
                Initialize();
                base.TraceEvent(eventCache, source, eventType, id, format, args);
            }
            catch (InvalidOperationException)
            {
                HandleException(eventCache, source, eventType, id, format, args);
            }
        }

        /// <summary>
        /// Trace event is Overriden to check the Log file Access.
        /// </summary>
        /// <param name="eventCache">TraceEventCache</param>
        /// <param name="source">Source</param>
        /// <param name="eventType">TraceEventType</param>
        /// <param name="id">Id</param>
        /// <param name="message">message string</param>
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            try
            {
                if (check == false && this.Location == LogFileLocation.Custom && !IsWritePermitted(this.CustomLocation))
                {
                    this.Location = LogFileLocation.LocalUserApplicationDirectory;
                    check = true;
                }
            }
            catch (ArgumentException)
            {
                this.Location = LogFileLocation.LocalUserApplicationDirectory;
                check = true;
            }
            message = this.FormatMessage(message);
            try
            {
                Initialize();
                this.TraceEventCustom(eventCache, source, eventType, id, message);
            }
            catch (InvalidOperationException)
            {
                HandleException(eventCache, source, eventType, id, message, null);
            }
        }

        /// <summary>
        /// Format the message with additional information
        /// </summary>
        /// <param name="message">Message to be logged</param>
        /// <returns>Formatted message</returns>
        protected virtual string FormatMessage(string message)
        {
            return string.Format("{0}  {1}", DateTime.Now.ToLocalTime().ToString(), message);
        }

        /// <summary>
        /// Trace event. Allows overriden behavior in inheriting classes
        /// </summary>
        /// <param name="eventCache"></param>
        /// <param name="source"></param>
        /// <param name="eventType"></param>
        /// <param name="id"></param>
        /// <param name="message"></param>
        protected virtual void TraceEventCustom(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            base.TraceEvent(eventCache, source, eventType, id, message);
        }

        private void RollOverLogFile()
        {
            //if maxfilecount not specified, return
            if (MaxFileCount == -1) return;

            logfiles.Enqueue(this.FullLogFileName);
            while (logfiles.Count > 0 && logfiles.Count >= MaxFileCount)
            {
                string filename = logfiles.Dequeue();
                if (System.IO.File.Exists(filename))
                {
                    try
                    {
                        System.IO.File.Delete(filename);
                    }
                    catch (Exception e)
                    {
                        EventLog.WriteEntry("application", string.Format("Delete of file {0} failed. Exception {1}", filename, e.ToString()),
                            EventLogEntryType.Error);
                        break;
                    }
                }
            }
            /* Change the base filename */
            this.BaseFileName = logFileName;
        }

        private void HandleException(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message, params object[] args)
        {
            RollOverLogFile();
            /* try Logging again */
            try
            {
                /* we need to call the TraceEvent of the base class. If we don't do the check 
				 * here and call the proper overloaded function from here, the check will happen
				 * inside base class and it will result in functions of this class getting called. */
                if (args == null)
                {
                    this.TraceEventCustom(eventCache, source, eventType, id, message);
                }
                else
                {
                    base.TraceEvent(eventCache, source, eventType, id, message, args);
                }
            }
            catch (Exception e)
            {
                try
                {
                    EventLog.WriteEntry("application",
                        string.Format("Exception while logging after rollover. Exception: {0}", e.ToString()),
                        EventLogEntryType.Error);
                    /* RollOver failed. So revert back to default DiskSpaceExhaustedBehavior. */
                    this.DiskSpaceExhaustedBehavior = DiskSpaceExhaustedOption.DiscardMessages;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Adding MaxFileCount to the suported attribute list
        /// </summary>
        /// <returns></returns>
        protected override string[] GetSupportedAttributes()
        {
            string[] sa = base.GetSupportedAttributes();
            if (sa == null) return new string[] { "MaxFileCount", "maxfilecount" };
            string[] sa2 = new string[sa.Length + 2];
            sa2[0] = "MaxFileCount";
            sa2[1] = "maxfilecount";
            Array.Copy(sa, 0, sa2, 2, sa.Length);
            return sa2;
        }

    }
#endif
}
