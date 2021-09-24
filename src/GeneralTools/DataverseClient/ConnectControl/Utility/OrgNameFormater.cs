using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
	/// <summary>
	/// Formats the org name for to append the Unique Name to the List View. 
	/// </summary>
	public class OrgNameFormater : IMultiValueConverter
	{
		/// <summary>
		/// Convert an In value to a string
		/// </summary>
		/// <param name="values"></param>
		/// <param name="targetType"></param>
		/// <param name="parameter"></param>
		/// <param name="culture"></param>
		/// <returns></returns>
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (values != null && values.Length == 2)
			{
				if (values[1] is string && !string.IsNullOrWhiteSpace((string)values[1]))
				{
					// if the Unique name and the Friendly name are identical, just return the friendly name. 
					// this will happen for most premise installs.
					if (((string)values[0]).Equals((string)values[1] , StringComparison.OrdinalIgnoreCase))
						return values[0];
					else
						return string.Format("{0} - {1}", (string)values[0], (string)values[1]);
				}
				else
					return values[0];
			}
			else
				if (values != null)
					return values[0];
			return string.Empty;
		}

		/// <summary>
		/// Not used. 
		/// </summary>
		/// <param name="value"></param>
		/// <param name="targetTypes"></param>
		/// <param name="parameter"></param>
		/// <param name="culture"></param>
		/// <returns></returns>
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return null;
		}
	}

}
