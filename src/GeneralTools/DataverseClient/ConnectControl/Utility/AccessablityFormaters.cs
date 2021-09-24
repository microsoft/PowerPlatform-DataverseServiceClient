using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
    /// <summary>
    /// Converter to convert Accessible names for radio buttons 
    /// </summary>
    public class RadioButtonAccessibleNameFormater : IMultiValueConverter
    {
        /// <summary>
        /// Converter to convert radio button names for Accessibility 
        /// </summary>
        /// <param name="values"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values != null && values.Count() > 1)
            {
                StringBuilder messageToBeRead = new StringBuilder();
                try
                {
                    foreach (var itm in values)
                    {
                        if (itm is string)
                        {
                            messageToBeRead.AppendFormat("{0} ", (string)itm);
                        }
                    }
                    if (messageToBeRead.Length > 0)
                        return messageToBeRead.ToString();
                    else
                        return string.Empty;
                }
                finally
                {
                    messageToBeRead.Clear();
                }

            }
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
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null; 
        }
    }
}
