namespace Microsoft.PowerPlatform.Dataverse.ConnectControl
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Reflection;
	using Utility;

	/// <summary>
	/// Static class for changing the CultureInfo of the ConnectControl
	/// </summary>
	public static class CultureUtils
	{
		private static CultureInfo _customUICulture { get; set; }
		
		/// <summary>
		/// Gets the CultureInfo to be used. If it was overridden by the caller, return that, else return the CurrentUICulture.
		/// </summary>
		internal static CultureInfo UICulture
		{
			get
			{
				if (null != _customUICulture)
				{
					return _customUICulture;
				}
				else
				{
					return CultureInfo.CurrentUICulture;
				}
			}
		}

		/// <summary>
		/// Allows caller to change the culture that will be used to render messages and resources.
		/// If this is not called, the default UI Culture will be used.
		/// This must be called before the ServerLoginControl is initialized or it will not take effect.
		/// </summary>
		/// <param name="languageCode">language code</param>
		public static void SetDisplayCulture(int languageCode)
		{
			LoginTracer tracer = new LoginTracer();
			try
			{
				CultureInfo culture = new CultureInfo(languageCode);
				if (SatelliteAssemblyExists(culture))
				{
					Properties.Resources.Culture = culture;
					Properties.Messages.Culture = culture;
					_customUICulture = culture;
				}
				else
				{
					tracer.Log(string.Format(CultureInfo.InvariantCulture, "Could not find satellite Resources assembly for Culture:{0} LCID:{1}. Defaulting to embedded resources.", culture.Name, languageCode), TraceEventType.Information);
				}
			}
			catch (ArgumentOutOfRangeException ex)
			{
				tracer.Log(string.Format(CultureInfo.InvariantCulture, "Could not create culture for language code {0}. Defaulting to CurrentUICulture.", languageCode), TraceEventType.Warning, ex);
			}
			catch (CultureNotFoundException ex)
			{
				tracer.Log(string.Format(CultureInfo.InvariantCulture, "Could not create culture for language code {0}. Defaulting to CurrentUICulture.", languageCode), TraceEventType.Warning, ex);
			}
		}

		private static bool SatelliteAssemblyExists(CultureInfo culture)
		{
			try
			{
				Assembly.GetExecutingAssembly().GetSatelliteAssembly(culture);
			}
			catch(FileNotFoundException)
			{
				return false;
			}
			return true;
		}
		
	}
}
