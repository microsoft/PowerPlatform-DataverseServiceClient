using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Trace setting store
	/// </summary>
	public class TraceSourceSettingStore
	{
		/// <summary>
		/// Source name of trace listner
		/// </summary>
		public static List<TraceSourceSetting> TraceSourceSettingsCollection { get; private set; }

		static TraceSourceSettingStore()
		{
			TraceSourceSettingsCollection = new List<TraceSourceSetting>();
		}

		/// <summary>
		///
		/// </summary>
		public static void AddTraceSettingsToStore(TraceSourceSetting listnerSettings)
		{
			Trace.AutoFlush = true;
			if (listnerSettings != null)
			{
				var settings = TraceSourceSettingsCollection.SingleOrDefault(x => String.Compare(x.SourceName, listnerSettings.SourceName, StringComparison.OrdinalIgnoreCase) == 0);
				if (settings != null) TraceSourceSettingsCollection.Remove(settings);
				TraceSourceSettingsCollection.Add(listnerSettings);
			}
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="sourceName"></param>
		/// <returns></returns>
		public static TraceSourceSetting GetTraceSourceSettings(string sourceName)
		{
			TraceSourceSetting settings = null;
			if (!string.IsNullOrEmpty(sourceName))
			{
				settings = TraceSourceSettingsCollection
					.SingleOrDefault(x => String.Compare(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase) == 0);
			}
			return settings;
		}

	}
}