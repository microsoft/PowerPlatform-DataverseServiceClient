using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// TraceLoggerBase Class.
	/// </summary>
	[LocalizableAttribute(false)]
#pragma warning disable CA1063 // Implement IDisposable Correctly
	public abstract class TraceLoggerBase : IDisposable
#pragma warning restore CA1063 // Implement IDisposable Correctly
	{

		#region Private Fields

		/// <summary>
		/// String Builder Info
		/// </summary>
		private StringBuilder _lastError = new StringBuilder();

		/// <summary>
		/// string _traceSourceName private field
		/// </summary>
		private string _traceSourceName;

		/// <summary>
		/// Last Exception
		/// </summary>
		private Exception _lastException = null;

		private TraceSource _source;

		#endregion

		#region Protected fields
		/// <summary>
		/// Trace source
		/// </summary>
		protected TraceSource Source
		{
			get
			{
				try
				{
					SourceLevels sourceLevel = _source.Switch.Level;
				}
				catch(Exception ex)
				{
					string errMsg = string.Format(CultureInfo.InvariantCulture,
					"Logging Provider Exception: {0}\nInnerEx: {1}\nStack:{2}\n",
					ex.Message, ex.InnerException != null ? ex.InnerException.Message : string.Empty, ex.StackTrace);
					try
					{
#if NET462
						//adding event log
						System.Diagnostics.EventLog.WriteEntry("application", errMsg, System.Diagnostics.EventLogEntryType.Error);
#endif
					}
					catch
					{
						//error in writing to event log
						string log = string.Format("UNABLE TO WRITE TO EVENT LOG FOR: {0}", errMsg);
						System.Diagnostics.Trace.WriteLine(log);
					}
					_source.Switch.Level = SourceLevels.Error;
				}
				return _source;
			}
			private set
			{
				_source = value;
			}
		}

		/// <summary>
		/// Trace Name
		/// </summary>
		protected string TraceSourceName
		{
			get
			{
				return _traceSourceName;
			}
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					throw new ArgumentNullException("TraceSourceName");
				}
				_traceSourceName = value;
			}
		}

		#endregion

		#region Properties
		/// <summary>
		/// Last Error from CRM
		/// </summary>
		public StringBuilder LastError
		{
			get { return _lastError; }
		}
		/// <summary>
		/// Last Exception from CRM
		/// </summary>
		public Exception LastException
		{
			get { return _lastException; }
			set { this._lastException = value; }
		}

		/// <summary>
		/// Current Trace level
		/// </summary>
		public SourceLevels CurrentTraceLevel
		{
			get { return Source.Switch.Level; }
		}

		#endregion

		#region Public Methods
		/// <summary>
		/// default TraceLoggerBase constructor
		/// </summary>
		protected TraceLoggerBase()
		{
			TraceListenerBroker.RegisterTraceLogger(this);
		}

		/// <summary>
		/// Initialize Trace Source
		/// </summary>
		protected void Initialize()
		{
			_source = new TraceSource(TraceSourceName);

			if (TraceSourceSettingStore.TraceSourceSettingsCollection.Count > 0)
			{
				RefreshListeners(TraceSourceSettingStore.TraceSourceSettingsCollection);
			}
		}

		/// <summary>
		/// Reset the last Stored Error
		/// </summary>
		public abstract void ResetLastError();

		/// <summary>
		/// Log a Message as an Information event.
		/// </summary>
		/// <param name="message"></param>
		public abstract void Log(string message);

		/// <summary>
		/// Log a Trace event
		/// </summary>
		/// <param name="message"></param>
		/// <param name="eventType"></param>
		public abstract void Log(string message, TraceEventType eventType);

		/// <summary>
		/// Log a Trace event
		/// </summary>
		/// <param name="message"></param>
		/// <param name="eventType"></param>
		/// <param name="exception"></param>
		public abstract void Log(string message, TraceEventType eventType, Exception exception);

		/// <summary>
		/// Logg an error with an Exception
		/// </summary>
		/// <param name="exception"></param>
		public abstract void Log(Exception exception);


		/// <summary>
		/// To refresh listeners
		/// </summary>
		/// <param name="traceSourceSettingCollection"></param>
		public void RefreshListeners(List<TraceSourceSetting> traceSourceSettingCollection)
		{
			if (traceSourceSettingCollection == null)
				throw new ArgumentNullException("Input param traceSourceSettingCollection cannot be null.");

			var traceSourceSetting = traceSourceSettingCollection.FirstOrDefault(x => String.Compare(x.SourceName, Source.Name, StringComparison.OrdinalIgnoreCase) == 0);
			if (traceSourceSetting != null && traceSourceSetting.TraceListeners.Any())
			{
				Source.Listeners.Clear();
				Source.Listeners.AddRange(traceSourceSetting.TraceListeners.Select(x => x.Value).ToArray());
				Source.Switch.Level = traceSourceSetting.TraceLevel;
			}
		}

		#region IDisposable Support

		/// <summary>
		///
		/// </summary>
#pragma warning disable CA1063 // Implement IDisposable Correctly
		public void Dispose()
#pragma warning restore CA1063 // Implement IDisposable Correctly
		{
			// Always need this to be called. 
			TraceListenerBroker.UnRegisterTraceLogger(this);
		}
		#endregion

		#endregion
	}
}