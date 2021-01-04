using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Parameter for delegate - RegisterdTraceListeners in TraceControlSettingsBase class
	/// </summary>
	public class TraceSourceSetting
	{
		/// <summary>
		/// Source name of trace listner
		/// </summary>
		public string SourceName { get; set; }

		/// <summary>
		/// Override Trace Level setting
		/// </summary>
		public SourceLevels TraceLevel { get; set; }

		/// <summary>
		/// List of trace listners
		/// </summary>
		public Dictionary<string, TraceListener> TraceListeners { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		private TraceSourceSetting()
		{
			TraceListeners = new Dictionary<string, TraceListener>();
		}

		/// <summary>
		/// Constructor
		/// </summary>
		public TraceSourceSetting(string sourceName, SourceLevels sourceLevels)
		{
			TraceListeners = new Dictionary<string, TraceListener>();
			this.SourceName = sourceName;
			this.TraceLevel = sourceLevels;
		}
	}
}
