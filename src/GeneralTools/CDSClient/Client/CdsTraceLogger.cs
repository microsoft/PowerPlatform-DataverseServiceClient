using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Globalization;
//using System.Web.Services.Protocols;
using System.ComponentModel;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Log Entry
	/// </summary>
	[LocalizableAttribute(false)]
	internal sealed class CdsTraceLogger : TraceLoggerBase
	{
		// Internal connection of exceptions since last clear. 
		private List<Exception> _ActiveExceptionsList;

		#region Properties
		/// <summary>
		/// Last Error from CRM
		/// </summary>
		public new string LastError
		{
			get { return base.LastError.ToString(); }
		}

		/// <summary>
		/// Default TraceSource Name
		/// </summary>
		public string DefaultTraceSourceName
		{
			get { return "Microsoft.PowerPlatform.Cds.Client.CdsServiceClient"; }
		}

		/// <summary>
		/// Collection of logs captured to date.
		/// </summary>
		public ConcurrentQueue<Tuple<DateTime, string>> Logs { get; private set; } = new ConcurrentQueue<Tuple<DateTime, string>>();

		/// <summary>
		/// Defines to the maximum amount of time in Minuets that logs will be kept in memory before being purged
		/// </summary>
		public TimeSpan LogRetentionDuration { get; set; } = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Enables or disabled in-memory log capture.
		/// Default is false.
		/// </summary>
		public bool EnabledInMemoryLogCapture { get; set; } = false;

		#endregion

		#region Public Methods

		/// <summary>
		/// Constructs the CdsTraceLogger class.
		/// </summary>
		/// <param name="traceSourceName"></param>
		public CdsTraceLogger(string traceSourceName = "")
			: base()
		{
			if (string.IsNullOrWhiteSpace(traceSourceName))
			{
				TraceSourceName = DefaultTraceSourceName;
			}
			else
			{
				TraceSourceName = traceSourceName;
			}

			_ActiveExceptionsList = new List<Exception>();

			base.Initialize();
		}

		public override void ResetLastError()
		{
			base.LastError.Remove(0, LastError.Length);
			LastException = null;
			_ActiveExceptionsList.Clear();
		}

		/// <summary>
		/// Clears log cache.
		/// </summary>
		public void ClearLogCache()
		{
			if (Logs != null)
			{
				Logs = new ConcurrentQueue<Tuple<DateTime, string>>();
			}
		}

		/// <summary>
		/// Log a Message
		/// </summary>
		/// <param name="message"></param>
		public override void Log(string message)
		{
			Source.TraceEvent(TraceEventType.Information, (int)TraceEventType.Information, message);
		}

		/// <summary>
		/// Log a Trace event
		/// </summary>
		/// <param name="message"></param>
		/// <param name="eventType"></param>
		public override void Log(string message, TraceEventType eventType)
		{
			TraceEvent(eventType, (int)eventType, message);
			if (eventType == TraceEventType.Error)
			{
				Log(message, eventType, new Exception(message));
			}
		}

		/// <summary>
		/// Log a Trace event
		/// </summary>
		/// <param name="message"></param>
		/// <param name="eventType"></param>
		/// <param name="exception"></param>
		public override void Log(string message, TraceEventType eventType, Exception exception)
		{
			if(exception == null && !String.IsNullOrEmpty(message))
			{
				exception = new Exception(message);
			}

			StringBuilder detailedDump = new StringBuilder();
			StringBuilder lastMessage = new StringBuilder();

			lastMessage.AppendLine(message); // Added to fix missing last error line. 
			detailedDump.AppendLine(message); // Added to fix missing error line. 

			if (!(exception != null && _ActiveExceptionsList.Contains(exception))) // Skip this line if its already been done. 
				GetExceptionDetail(exception, detailedDump, 0, lastMessage);

			TraceEvent(eventType, (int)eventType, detailedDump.ToString());
			if (eventType == TraceEventType.Error)
			{
				base.LastError.Append(lastMessage.ToString());
				if (!(exception != null && _ActiveExceptionsList.Contains(exception))) // Skip this line if its already been done. 
				{
					// check and or alter the exception is its and HTTPOperationExecption. 
					if (exception is HttpOperationException httpOperationException)
					{
						JObject contentBody = JObject.Parse(httpOperationException.Response.Content);
						Utils.CdsClientOperationException webApiExcept = new Utils.CdsClientOperationException(string.IsNullOrEmpty(contentBody["error"]["message"]?.ToString()) ? "Not Provided" : GetFirstLineFromString(contentBody["error"]["message"]?.ToString()).Trim(), httpOperationException);
						LastException = webApiExcept;
					}
					else
						LastException = exception;
				}
			}
			_ActiveExceptionsList.Add(exception);

			detailedDump.Clear();
			lastMessage.Clear();

		}

		/// <summary>
		/// Log an error with an Exception
		/// </summary>
		/// <param name="exception"></param>
		public override void Log(Exception exception)
		{
			if (exception != null && _ActiveExceptionsList.Contains(exception))
				return;  // allready logged this one .

			StringBuilder detailedDump = new StringBuilder();
			StringBuilder lastMessage = new StringBuilder();
			GetExceptionDetail(exception, detailedDump, 0, lastMessage);
			TraceEvent(TraceEventType.Error, (int)TraceEventType.Error, detailedDump.ToString());
			base.LastError.Append(lastMessage.ToString());
			LastException = exception;

			_ActiveExceptionsList.Add(exception);

			detailedDump.Clear();
			lastMessage.Clear();
		}

		/// <summary>
		/// Logs data to memory.
		/// </summary>
		/// <param name="eventType"></param>
		/// <param name="id"></param>
		/// <param name="message"></param>
		private void TraceEvent(TraceEventType eventType, int id, string message)
		{
			Source.TraceEvent(eventType, id, message);

			if (EnabledInMemoryLogCapture)
			{
				Logs.Enqueue(Tuple.Create<DateTime, string>(DateTime.UtcNow,
					string.Format(CultureInfo.InvariantCulture, "[{0}][{1}] {2}", eventType, id, message)));

				DateTime expireDateTime = DateTime.UtcNow.Subtract(LogRetentionDuration);
				bool CleanUpLog = true;
				while (CleanUpLog)
				{
					Tuple<DateTime, string> peekOut = null;
					if (Logs.TryPeek(out peekOut))
					{
						if (peekOut.Item1 <= expireDateTime)
						{
							Tuple<DateTime, string> tos;
							Logs.TryDequeue(out tos);
							Debug.WriteLine($"Flushing LogEntry from memory: {tos.Item2}");  // Write flush events out to debug log. 
							tos = null;
						}
						else
						{
							CleanUpLog = false;
						}
					}
					else
					{
						CleanUpLog = false;
					}
				}
			}
		}

		#endregion

		/// <summary>
		/// Disassembles the Exception into a readable block
		/// </summary>
		/// <param name="objException">Exception to work with</param>
		/// <param name="sw">Writer to write too</param>
		/// <param name="level">depth</param>
		/// <param name="lastErrorMsg">Last Writer to write too</param>
		private void GetExceptionDetail(object objException, StringBuilder sw, int level, StringBuilder lastErrorMsg)
		{
			if (objException == null)
				return;

			//if (objException is SoapException)
			//{
			//	SoapException soapEx = (SoapException)objException;
			//	FormatExceptionMessage(
			//	soapEx.Source != null ? soapEx.Source.ToString().Trim() : "Not Provided",
			//	soapEx.TargetSite != null ? soapEx.TargetSite.Name.ToString() : "Not Provided",
			//	string.IsNullOrEmpty(soapEx.Message) ? "Not Provided" : soapEx.Message.ToString().Trim(),
			//	string.IsNullOrEmpty(soapEx.StackTrace) ? "Not Provided" : soapEx.StackTrace.ToString().Trim()
			//	, sw, level);

			//	lastErrorMsg.Append(string.IsNullOrEmpty(soapEx.Message) ? "Not Provided" : soapEx.Message.ToString().Trim());

			//	if (lastErrorMsg.Length > 0 && soapEx.InnerException != null)
			//		lastErrorMsg.Append(" => ");

			//	level++;
			//	if (soapEx.InnerException != null)
			//		GetExceptionDetail(soapEx.InnerException, sw, level, lastErrorMsg);

			//}
			//else
			if (objException is FaultException<OrganizationServiceFault>)
			{
				FaultException<OrganizationServiceFault> OrgFault = (FaultException<OrganizationServiceFault>)objException;
				string ErrorDetail = GenerateOrgErrorDetailsInfo(OrgFault.Detail.ErrorDetails);
				FormatExceptionMessage(
				OrgFault.Source != null ? OrgFault.Source.ToString().Trim() : "Not Provided",
				OrgFault.TargetSite != null ? OrgFault.TargetSite.Name.ToString() : "Not Provided",
				OrgFault.Detail != null ? string.Format(CultureInfo.InvariantCulture, "Message: {0}\nErrorCode: {1}\nTrace: {2}{3}", OrgFault.Detail.Message, OrgFault.Detail.ErrorCode, OrgFault.Detail.TraceText, string.IsNullOrEmpty(ErrorDetail) ? "" : $"\n{ErrorDetail}") :
				string.IsNullOrEmpty(OrgFault.Message) ? "Not Provided" : OrgFault.Message.ToString().Trim(),
				string.IsNullOrEmpty(OrgFault.StackTrace) ? "Not Provided" : OrgFault.StackTrace.ToString().Trim()
				, sw, level);

				lastErrorMsg.Append(OrgFault.Detail != null ? OrgFault.Detail.Message :
				string.IsNullOrEmpty(OrgFault.Message) ? string.Empty : OrgFault.Message.ToString().Trim());

				if (lastErrorMsg.Length > 0 && (OrgFault.InnerException != null || OrgFault.Detail != null && OrgFault.Detail.InnerFault != null))
					lastErrorMsg.Append(" => ");

				level++;
				if ((OrgFault.InnerException != null || OrgFault.Detail != null && OrgFault.Detail.InnerFault != null))
					GetExceptionDetail(OrgFault.Detail != null && OrgFault.Detail.InnerFault != null ? OrgFault.Detail.InnerFault : (object)OrgFault.InnerException,
					sw, level, lastErrorMsg);

				return;

			}
			else
			{
				if (objException is OrganizationServiceFault)
				{
					OrganizationServiceFault oFault = (OrganizationServiceFault)objException;
					string ErrorDetail = GenerateOrgErrorDetailsInfo(oFault.ErrorDetails);
					FormatOrgFaultMessage(
							string.Format(CultureInfo.InvariantCulture, "Message: {0}\nErrorCode: {1}\nTrace: {2}{3}", oFault.Message, oFault.ErrorCode, oFault.TraceText, string.IsNullOrEmpty(ErrorDetail) ? "" : $"\n{ErrorDetail}"),
							oFault.Timestamp.ToString(),
							oFault.ErrorCode.ToString(),
							string.IsNullOrEmpty(oFault.TraceText) ? "Not Provided" : oFault.TraceText.ToString().Trim(), sw, level);

					level++;

					lastErrorMsg.Append(oFault.Message);
					if (lastErrorMsg.Length > 0 && oFault.InnerFault != null)
						lastErrorMsg.Append(" => ");

					if (oFault.InnerFault != null)
						GetExceptionDetail(oFault.InnerFault, sw, level, lastErrorMsg);

					return;

				}
				else
				{
					if (objException is HttpOperationException httpOperationException)
					{
						JObject contentBody = JObject.Parse(httpOperationException.Response.Content);

						FormatExceptionMessage(
						httpOperationException.Source != null ? httpOperationException.Source.ToString().Trim() : "Not Provided",
						httpOperationException.TargetSite != null ? httpOperationException.TargetSite.Name?.ToString() : "Not Provided",
						string.IsNullOrEmpty(contentBody["error"]["message"]?.ToString()) ? "Not Provided" : GetFirstLineFromString(contentBody["error"]["message"]?.ToString()).Trim(),
						string.IsNullOrEmpty(contentBody["error"]["stacktrace"]?.ToString()) ? "Not Provided" : contentBody["error"]["stacktrace"]?.ToString().Trim()
						, sw, level);

						lastErrorMsg.Append(string.IsNullOrEmpty(httpOperationException.Message) ? "Not Provided" : httpOperationException.Message.ToString().Trim());

						// WebEx currently only returns 1 leve of error. 
						var InnerError = contentBody["error"]["innererror"];
						if (lastErrorMsg.Length > 0 && InnerError != null)
						{
							level++;
							lastErrorMsg.Append(" => ");
							FormatExceptionMessage(
								httpOperationException.Source != null ? httpOperationException.Source.ToString().Trim() : "Not Provided",
								httpOperationException.TargetSite != null ? httpOperationException.TargetSite.Name?.ToString() : "Not Provided",
								string.IsNullOrEmpty(InnerError["message"]?.ToString()) ? "Not Provided" : GetFirstLineFromString(InnerError["message"]?.ToString()).Trim(),
								string.IsNullOrEmpty(InnerError["stacktrace"]?.ToString()) ? "Not Provided" : InnerError["stacktrace"]?.ToString().Trim()
								, sw, level);
						}
					}
					else
					if (objException is Exception)
					{
						Exception generalEx = (Exception)objException;
						FormatExceptionMessage(
						generalEx.Source != null ? generalEx.Source.ToString().Trim() : "Not Provided",
						generalEx.TargetSite != null ? generalEx.TargetSite.Name.ToString() : "Not Provided",
						string.IsNullOrEmpty(generalEx.Message) ? "Not Provided" : generalEx.Message.ToString().Trim(),
						string.IsNullOrEmpty(generalEx.StackTrace) ? "Not Provided" : generalEx.StackTrace.ToString().Trim()
						, sw, level);

						lastErrorMsg.Append(string.IsNullOrEmpty(generalEx.Message) ? "Not Provided" : generalEx.Message.ToString().Trim());

						if (lastErrorMsg.Length > 0 && generalEx.InnerException != null)
							lastErrorMsg.Append(" => ");

						level++;
						if (generalEx.InnerException != null)
							GetExceptionDetail(generalEx.InnerException, sw, level, lastErrorMsg);
					}
				}
			}
			return;
		}

		/// <summary>
		/// returns the first line from the text block.
		/// </summary>
		/// <param name="textBlock"></param>
		/// <returns></returns>
		internal static string GetFirstLineFromString(string textBlock)
        {
			if (!string.IsNullOrEmpty(textBlock))
			{
				if (textBlock.Contains(Environment.NewLine))
					return textBlock.Substring(0, textBlock.IndexOf(Environment.NewLine));
			}
			return textBlock;
        }

		/// <summary>
		/// Formats the detail collection from a service exception.
		/// </summary>
		/// <param name="errorDetails"></param>
		/// <returns></returns>
		private static string GenerateOrgErrorDetailsInfo(ErrorDetailCollection errorDetails)
		{
			if (errorDetails != null && errorDetails.Count > 0)
			{
				StringBuilder sw = new StringBuilder();
				sw.AppendLine("Error Details\t:");
				foreach (var itm in errorDetails)
				{
					string valueText = itm.Value != null ? itm.Value.ToString() : "Not Set";
					sw.AppendLine($"{itm.Key}\t: {valueText}");
				}
				return sw.ToString();
			}
			return string.Empty;
		}

		/// <summary>
		/// Creates the exception message.
		/// </summary>
		/// <param name="source">Source of Exception</param>
		/// <param name="targetSite">Target of Exception</param>
		/// <param name="message">Exception Message</param>
		/// <param name="stackTrace">StackTrace</param>
		/// <param name="sw">Writer to write too</param>
		/// <param name="level">Depth of Exception</param>
		private static void FormatExceptionMessage(string source, string targetSite, string message, string stackTrace, StringBuilder sw, int level)
		{
			if (level != 0)
				sw.AppendLine(string.Format(CultureInfo.InvariantCulture, "Inner Exception Level {0}\t: ", level));
			sw.AppendLine("Source\t: " + source);
			sw.AppendLine("Method\t: " + targetSite);
			sw.AppendLine("Date\t: " + DateTime.Now.ToShortDateString());
			sw.AppendLine("Time\t: " + DateTime.Now.ToLongTimeString());
			sw.AppendLine("Error\t: " + message);
			sw.AppendLine("Stack Trace\t: " + stackTrace);
			sw.AppendLine("======================================================================================================================");
		}

		/// <summary>
		/// Formats an Exception specific to an organization fault.
		/// </summary>
		/// <param name="message">Exception Message</param>
		/// <param name="timeOfEvent">Time occurred</param>
		/// <param name="errorCode">Error code of message</param>
		/// <param name="traceText">Message Text</param>
		/// <param name="sw">Writer to write too</param>
		/// <param name="level">Depth</param>
		private static void FormatOrgFaultMessage(string message, string timeOfEvent, string errorCode, string traceText, StringBuilder sw, int level)
		{
			if (level != 0)
				sw.AppendLine(string.Format(CultureInfo.InvariantCulture, "Inner Exception Level {0}\t: ", level));
			sw.AppendLine("==OrganizationServiceFault Info=======================================================================================");
			sw.AppendLine("Error\t: " + message);
			sw.AppendLine("Time\t: " + timeOfEvent);
			sw.AppendLine("ErrorCode\t: " + errorCode);
			sw.AppendLine("Date\t: " + DateTime.Now.ToShortDateString());
			sw.AppendLine("Time\t: " + DateTime.Now.ToLongTimeString());
			sw.AppendLine("Trace\t: " + traceText);
			sw.AppendLine("======================================================================================================================");
		}
	}

	/// <summary>
	/// This class provides an override for the default trace settings.
	/// These settings must be set before the components in the control are used for them to be effective.
	/// </summary>
	public class TraceControlSettings
	{
		private static string _traceSourceName = "Microsoft.PowerPlatform.Cds.Client.CdsServiceClient";

		/// <summary>
		/// Returns the Registered Trace Listeners in the override object.
		/// </summary>
		internal static Dictionary<string, TraceListener> RegisterdTraceListeners
		{
			get
			{
				return TraceSourceSettingStore.GetTraceSourceSettings(_traceSourceName) != null ?
					TraceSourceSettingStore.GetTraceSourceSettings(_traceSourceName).TraceListeners : null;
			}
		}
		/// <summary>
		/// Override Trace Level setting.
		/// </summary>
		public static SourceLevels TraceLevel { get; set; }
		/// <summary>
		/// Builds the base trace settings
		/// </summary>

		static TraceControlSettings()
		{
			TraceLevel = SourceLevels.Off;
		}

		/// <summary>
		/// Closes any trace listeners that were configured
		/// </summary>
		public static void CloseListeners()
		{
			if (RegisterdTraceListeners != null && RegisterdTraceListeners.Count > 0)
				foreach (TraceListener itm in RegisterdTraceListeners.Values)
				{
					itm.Close();
				}
		}
		/// <summary>
		/// Adds a listener to the trace listen array
		/// </summary>
		/// <param name="listenerToAdd">Trace Listener you wish to add</param>
		/// <returns>true on success, false on fail.</returns>
		public static bool AddTraceListener(TraceListener listenerToAdd)
		{
			try
			{
				Trace.AutoFlush = true;
				var traceSourceSetting = TraceSourceSettingStore.GetTraceSourceSettings(_traceSourceName);
				if (traceSourceSetting == null)
				{
					traceSourceSetting = new TraceSourceSetting(_traceSourceName, TraceLevel);
				}
				if (traceSourceSetting.TraceListeners == null)
					traceSourceSetting.TraceListeners = new Dictionary<string, TraceListener>();

				if (traceSourceSetting.TraceListeners.ContainsKey(listenerToAdd.Name))
					return true;

				traceSourceSetting.TraceListeners.Add(listenerToAdd.Name, listenerToAdd);
				TraceSourceSettingStore.AddTraceSettingsToStore(traceSourceSetting);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}

}