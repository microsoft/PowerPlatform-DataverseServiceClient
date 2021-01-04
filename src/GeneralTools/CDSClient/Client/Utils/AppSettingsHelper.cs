using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Utils
{
	/// <summary>
	/// Manages reading settings from app.config of associated files.
	/// </summary>
	internal static class AppSettingsHelper
	{
		/// <summary>
		/// Reads app setting from config file; if not found, returns <param name="defaultValue">the default value</param>.
		/// </summary>
		/// <param name="key">The setting key</param>
		/// <param name="logSink">Logger to use if available, else will be created and used for this session only</param>
		/// <return>Returns the setting value or default value.</return>
		public static T GetAppSetting<T>(string key, T defaultValue, CdsTraceLogger logSink = null)
		{
			bool isLogEntryCreatedLocaly = false;
			try
			{
				var value = System.Configuration.ConfigurationManager.AppSettings[key];
				if (value == null)
				{
					return defaultValue;
				}

				return ConvertFromInvariantString<T>(value);
			}
			catch(Exception ex)
			{
				if (logSink == null) // building on first use to optimize 
				{
					logSink = new CdsTraceLogger();
					isLogEntryCreatedLocaly = true;
				}
				logSink.Log($"Failed to read {key} from AppSettings, failed with message: {ex.Message}.  Using default value", System.Diagnostics.TraceEventType.Warning);
				return defaultValue;
			}
			finally
			{
				if (isLogEntryCreatedLocaly)
					logSink.Dispose();
			}
		}

		private static T ConvertFromInvariantString<T>(string value)
		{
			var typeOfT = typeof(T);
			var typeConverter = System.ComponentModel.TypeDescriptor.GetConverter(typeOfT);
			if (!typeConverter.CanConvertFrom(typeof(string)))
			{
				throw new ArgumentException("Unable to convert from a string to target type " + typeOfT + ".");
			}

			return (T)typeConverter.ConvertFromInvariantString(value);
		}

		/// <summary>
		/// Picks up a string value from the app config and converts it to the string value version based on the format provided.
		/// </summary>
		/// <param name="key">Key to lookup from the appconfig</param>
		/// <param name="format">TimeSpan format to convert from ( "d" = days, "m" = Minuets , "h" = hours , "s" = seconds , "ms" = Milliseconds ) </param>
		/// <param name="defaultValue">default value to use of the key is not found</param>
		/// <param name="logSink">Logger to use if available, else will be created and used for this session only</param>
		/// <returns>Determined timeSpan value.</returns>
		public static TimeSpan GetAppSettingTimeSpan(string key , TimeSpanFromKey format , TimeSpan defaultValue, CdsTraceLogger logSink = null)
		{
			bool isLogEntryCreatedLocaly = false;
			try
			{
				var value = System.Configuration.ConfigurationManager.AppSettings[key];
				if (value == null)
				{
					return defaultValue;
				}

				double incomingValue;
				if (!double.TryParse(value, out incomingValue))
				{
					if (logSink == null) // building on first use to optimize 
					{
						logSink = new CdsTraceLogger();
						isLogEntryCreatedLocaly = true;
					}
					logSink.Log($"Failed to read {key} from AppSettings, Value {incomingValue} cannot be parsed to a double.  Using default value", System.Diagnostics.TraceEventType.Warning);
					return defaultValue;
				}

				switch (format)
				{
					case TimeSpanFromKey.Minutes:
						return TimeSpan.FromMinutes(incomingValue);
					case TimeSpanFromKey.Hours:
						return TimeSpan.FromHours(incomingValue);
					case TimeSpanFromKey.Seconds:
						return TimeSpan.FromSeconds(incomingValue);
					case TimeSpanFromKey.Milliseconds:
						return TimeSpan.FromMilliseconds(incomingValue);
					case TimeSpanFromKey.Days:
						return TimeSpan.FromDays(incomingValue);
					default:
						return defaultValue;
				}
			}
			finally
			{
				if (isLogEntryCreatedLocaly)
					logSink.Dispose();
			}
		}

		/// <summary>
		/// Formatting Value for Text to TimeSpan conversion.
		/// </summary>
		public enum TimeSpanFromKey
		{
			Minutes,
			Hours,
			Seconds,
			Milliseconds,
			Days
		}
	}
}


