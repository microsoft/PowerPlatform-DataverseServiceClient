//===================================================================================
// Microsoft – subject to the terms of the Microsoft EULA and other agreements
// Microsoft.PowerPlatform.Dataverse.WebResourceUtility
// copyright 2003-2012 Microsoft Corp.
//
// Tracing Interface.
//
//===================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.ComponentModel;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace Microsoft.PowerPlatform.Dataverse.WebResourceUtility
{
	/// <summary/>
	public class TraceLogger : TraceLoggerBase
	{
		#region ReadOnlyFields
		private static string _defaultTraceSourceName = "Microsoft.PowerPlatform.Dataverse.WebResourceUtility";
		#endregion

		#region Public Methods

		/// <summary>
		/// TraceLogger Method
		/// </summary>
		/// <param name="traceSourceName"></param>
		public TraceLogger(string traceSourceName = "")
			: base()
		{
			if (string.IsNullOrWhiteSpace(traceSourceName))
			{
				TraceSourceName = _defaultTraceSourceName;
			}
			else
			{
				TraceSourceName = traceSourceName;
			}
			base.Initialize();
		}

		/// <summary/>
		public override void ResetLastError()
		{
			LastError.Remove(0, LastError.Length);
			LastException = null;
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
			Source.TraceEvent(eventType, (int)eventType, message);
			if (eventType == TraceEventType.Error)
			{
				LastError += message;
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
			StringBuilder sbException = new StringBuilder();
			sbException.AppendLine("Message: " + message);
			LogExceptionToFile(exception, sbException, 0);
			if (sbException.Length > 0)
				Source.TraceEvent(TraceEventType.Error, (int)TraceEventType.Error, sbException.ToString());

			Source.TraceEvent(eventType, (int)eventType, sbException.ToString());
			if (eventType == TraceEventType.Error)
			{
				LastError += sbException.ToString();
				LastException = exception;
			}
		}

		/// <summary>
		/// Log an error with an Exception
		/// </summary>
		/// <param name="exception"></param>
		public override void Log(Exception exception)
		{
			//string message = null;

			StringBuilder sbException = new StringBuilder();
			LogExceptionToFile(exception, sbException, 0);
			if (sbException.Length > 0)
				Source.TraceEvent(TraceEventType.Error, (int)TraceEventType.Error, sbException.ToString());

			LastError += sbException.ToString();
			LastException = exception;

		}

		/// <summary>
		/// Logs the error text to the stream
		/// </summary>
		/// <param name="objException">Exception to be written</param>
		/// <param name="sw">Stream writer to use to write the exception</param>
		/// <param name="level">level of the exception, this deals with inner exceptions</param>
		private static void LogExceptionToFile(Exception objException, StringBuilder sw, int level)
		{
			if (level != 0)
				sw.AppendLine(string.Format("Inner Exception Level {0}\t: ", level));

			sw.AppendLine("Source\t: " +
			(objException.Source != null ? objException.Source.ToString().Trim() : "Not Provided"));
			sw.AppendLine("Method\t: " +
			(objException.TargetSite != null ? objException.TargetSite.Name.ToString() : "Not Provided"));
			sw.AppendLine("Date\t: " +
			DateTime.Now.ToLongTimeString());
			sw.AppendLine("Time\t: " +
			DateTime.Now.ToShortDateString());
			sw.AppendLine("Error\t: " +
			(string.IsNullOrEmpty(objException.Message) ? "Not Provided" : objException.Message.ToString().Trim()));
			sw.AppendLine("Stack Trace\t: " +
			(string.IsNullOrEmpty(objException.StackTrace) ? "Not Provided" : objException.StackTrace.ToString().Trim()));
			sw.AppendLine("======================================================================================================================");

			level++;
			if (objException.InnerException != null)
				LogExceptionToFile(objException.InnerException, sw, level);

		}

		#endregion
	}

	/// <summary> 
	/// This class provides an override for the default trace settings.  
	/// These settings must be set before the components in the control are used for them to be effective.  
	/// </summary> 
	public class TraceControlSettings
	{

		private static string _traceSourceName = "Microsoft.PowerPlatform.Dataverse.WebResourceUtility";

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
				// Failed to add here.  return false;  
				return false;
			}
		}
	}
}
