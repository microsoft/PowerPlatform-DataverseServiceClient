using System;
using System.Diagnostics;
using System.Linq;
using System.Web.Services.Protocols;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using System.ServiceModel;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
	/// <summary>
	/// Tracing class to provide real time diagnostics for the connection manager process. 
	/// This class allows tracing to be enabled and configured on a deployed client.   
	/// </summary>
	internal class LoginTracer : TraceLoggerBase
	{
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
			get { return "Microsoft.PowerPlatform.Dataverse.ConnectControl"; }
		}

		#endregion

		#region Public Methods
		/// <summary> 
		/// Creates a new trace source for the CRMConnectControl 
		/// </summary> 
		/// <param name="traceSourceName">Name of the source, or empty</param> 
		public LoginTracer(string traceSourceName = "")
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
			base.Initialize();
		}

		public override void ResetLastError()
		{
            if (base.LastError.Length > 0)
                base.LastError = base.LastError.Remove(0, LastError.Length - 1);
            LastException = null;
		}


		/// <summary>
		/// Log a Message 
		/// </summary>
		/// <param name="message"></param>
		public override void Log(string message)
		{
			WriteTraceEvent(TraceEventType.Information, (int)TraceEventType.Information, message);
		}

		/// <summary>
		/// Log a Trace event 
		/// </summary>
		/// <param name="message"></param>
		/// <param name="eventType"></param>
		public override void Log(string message, TraceEventType eventType)
		{
			WriteTraceEvent(eventType, (int)eventType, message);
			if (eventType == TraceEventType.Error)
			{
				base.LastError += message;
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

			StringBuilder detailedDump = new StringBuilder();
			StringBuilder lastMessage = new StringBuilder();

			detailedDump.AppendLine(string.Format("Error Message: {0}", message));
			GetExceptionDetail(exception, detailedDump, 0, lastMessage);

			WriteTraceEvent(eventType, (int)eventType, detailedDump.ToString());

			if (eventType == TraceEventType.Error)
			{
				base.LastError += lastMessage.ToString();
				LastException = exception;
			}

			detailedDump.Clear();
			lastMessage.Clear();
		}

		/// <summary>
		/// Log an error with an Exception
		/// </summary>
		/// <param name="exception"></param>
		public override void Log(Exception exception)
		{
			string message = null;

			StringBuilder detailedDump = new StringBuilder();
			StringBuilder lastMessage = new StringBuilder();
			GetExceptionDetail(exception, detailedDump, 0, lastMessage);

			WriteTraceEvent(TraceEventType.Error, (int)TraceEventType.Error, message);

			base.LastError += message;
			LastException = exception;

			detailedDump.Clear();
			lastMessage.Clear();
		}
		
		#region Private

		/// <summary>
		/// Write trace log.
		/// </summary>
		/// <param name="evntType"></param>
		/// <param name="traceId"></param>
		/// <param name="message"></param>
		private void WriteTraceEvent(TraceEventType evntType, int traceId, string message)
		{
			try
			{
				Source.TraceEvent(evntType, traceId, message);

			}
			catch (Exception ex)
			{
				Trace.WriteLine(string.Format("failed to write trace event : {0}", ex.Message));
			}
		}

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

			if (objException is SoapException)
			{
				SoapException soapEx = (SoapException)objException;
				FormatExceptionMessage(
				  soapEx.Source != null ? soapEx.Source.ToString().Trim() : "Not Provided",
				  soapEx.TargetSite != null ? soapEx.TargetSite.Name.ToString() : "Not Provided",
				  string.IsNullOrEmpty(soapEx.Message) ? "Not Provided" : soapEx.Message.ToString().Trim(),
				  string.IsNullOrEmpty(soapEx.StackTrace) ? "Not Provided" : soapEx.StackTrace.ToString().Trim()
				  , sw, level);

				lastErrorMsg.Append(string.IsNullOrEmpty(soapEx.Message) ? "Not Provided" : soapEx.Message.ToString().Trim());

				if (lastErrorMsg.Length > 0 && soapEx.InnerException != null)
					lastErrorMsg.Append(" => ");

				level++;
				if (soapEx.InnerException != null)
					GetExceptionDetail(soapEx.InnerException, sw, level, lastErrorMsg);

			}
			else
				if (objException is FaultException<OrganizationServiceFault>)
				{
					FaultException<OrganizationServiceFault> OrgFault = (FaultException<OrganizationServiceFault>)objException;
					FormatExceptionMessage(
						OrgFault.Source != null ? OrgFault.Source.ToString().Trim() : "Not Provided",
						OrgFault.TargetSite != null ? OrgFault.TargetSite.Name.ToString() : "Not Provided",
						OrgFault.Detail != null ? string.Format(CultureInfo.CurrentCulture, "Message: {0}\nErrorCode: {1}\nTrace: {2}", OrgFault.Detail.Message, OrgFault.Detail.ErrorCode, OrgFault.Detail.TraceText) :
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
					if (objException is OrganizationServiceFault)
					{
						OrganizationServiceFault oFault = (OrganizationServiceFault)objException;
						FormatOrgFaultMessage(
							string.IsNullOrEmpty(oFault.Message) ? "Not Provided" : oFault.Message.ToString().Trim(),
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
			return;
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
		private void FormatExceptionMessage(string source, string targetSite, string message, string stackTrace, StringBuilder sw, int level)
		{
			if (level != 0)
				sw.AppendLine(string.Format(CultureInfo.CurrentCulture, "Inner Exception Level {0}\t: ", level));
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
		private void FormatOrgFaultMessage(string message, string timeOfEvent, string errorCode, string traceText, StringBuilder sw, int level)
		{
			if (level != 0)
				sw.AppendLine(string.Format(CultureInfo.CurrentCulture, "Inner Exception Level {0}\t: ", level));
			sw.AppendLine("==OrganizationServiceFault Info=======================================================================================");
			sw.AppendLine("Error\t: " + message);
			sw.AppendLine("Time\t: " + timeOfEvent);
			sw.AppendLine("ErrorCode\t: " + errorCode);
			sw.AppendLine("Date\t: " + DateTime.Now.ToShortDateString());
			sw.AppendLine("Time\t: " + DateTime.Now.ToLongTimeString());
			sw.AppendLine("Trace\t: " + traceText);
			sw.AppendLine("======================================================================================================================");
		}



		#endregion

		#endregion
	}

	/// <summary> 
	/// This class provides an override for the default trace settings.  
	/// These settings must be set before the components in the control are used for them to be effective.  
	/// </summary> 
	public class TraceControlSettings
	{
		private static string _traceSourceName = "Microsoft.PowerPlatform.Dataverse.ConnectControl";

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
			var traceListeners = RegisterdTraceListeners;
			if (traceListeners != null && traceListeners.Count > 0)
				foreach (TraceListener itm in traceListeners.Values)
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
				// Failed to add here.  return false;  
				return false;
			}
		}
	}
}
