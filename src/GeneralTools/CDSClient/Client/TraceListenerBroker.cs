using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Refresh listener delegate
	/// </summary>
	/// <param name="listenerCollection"></param>
	public delegate void RefreshListenerDelegate(List<TraceSourceSetting> listenerCollection);

	/// <summary>
	/// Trace listener broker class
	/// </summary>
	public sealed class TraceListenerBroker
	{
		private static event RefreshListenerDelegate  refreshListenerObject = null ;
		private static object logLock = new object();

		/// <summary>
		/// Method to register trace logger
		/// </summary>
		/// <param name="traceLogger"></param>
		public static void RegisterTraceLogger(TraceLoggerBase traceLogger)
		{
			lock (logLock)
			{
				refreshListenerObject += new RefreshListenerDelegate(traceLogger.RefreshListeners);
			}
		}

		/// <summary>
		/// Method to un register trace logger
		/// </summary>
		/// <param name="traceLogger"></param>
		public static void UnRegisterTraceLogger(TraceLoggerBase traceLogger)
		{
			lock (logLock)
			{
				if (refreshListenerObject != null)
				 refreshListenerObject -= new RefreshListenerDelegate(traceLogger.RefreshListeners);
			}
		}

		/// <summary>
		/// Method to refresh listeners for all the registered trace loggers
		/// </summary>
		public static void PublishTraceListeners()
		{
			if (refreshListenerObject != null)
			{
				refreshListenerObject(TraceSourceSettingStore.TraceSourceSettingsCollection);
			}
		}
	}
}
