using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions
{
    internal static class BaseTypeExtensions
    {
        /// <summary>
        /// Enum extension
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumName"></param>
        /// <returns>Enum Value</returns>
        public static T ToEnum<T>(this string enumName)
        {
            return (T)((object)Enum.Parse(typeof(T), enumName));
        }
        /// <summary>
        /// Converts a int to a Enum of the requested type (T)
        /// </summary>
        /// <typeparam name="T">Enum Type to translate too</typeparam>
        /// <param name="enumValue">Int Value too translate.</param>
        /// <returns>Enum of Type T</returns>
        public static T ToEnum<T>(this int enumValue)
        {
            return enumValue.ToString().ToEnum<T>();
        }
        /// <summary>
        /// Converts a ; separated string into a dictionary
        /// </summary>
        /// <param name="connectionString">String to parse</param>
        /// <returns>Dictionary of properties from the connection string</returns>
        public static IDictionary<string, string> ToDictionary(this string connectionString)
        {
            try
            {
                DbConnectionStringBuilder source = new DbConnectionStringBuilder
                {
                    ConnectionString = connectionString
                };

                Dictionary<string, string> dictionary = source.Cast<KeyValuePair<string, object>>().
                    ToDictionary((KeyValuePair<string, object> pair) => pair.Key,
                    (KeyValuePair<string, object> pair) => pair.Value != null ? pair.Value.ToString() : string.Empty);
                return new Dictionary<string, string>(dictionary, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                //ignore
            }
            return new Dictionary<string, string>();

        }
        /// <summary>
        /// Extension to support formating a string
        /// </summary>
        /// <param name="format">Formatting pattern</param>
        /// <param name="args">Argument collection</param>
        /// <returns>Formated String</returns>
        public static string FormatWith(this string format, params object[] args)
        {
            return format.FormatWith(CultureInfo.InvariantCulture, args);
        }
        /// <summary>
        /// Extension to get the first item in a dictionary if the dictionary contains the key.
        /// </summary>
        /// <typeparam name="TKey">Type to return</typeparam>
        /// <param name="dictionary">Dictionary to search</param>
        /// <param name="keys">Collection of Keys to find.</param>
        /// <returns></returns>
        public static string FirstNotNullOrEmpty<TKey>(this IDictionary<TKey, string> dictionary, params TKey[] keys)
        {
            return (
                from key in keys
                where dictionary.ContainsKey(key) && !string.IsNullOrEmpty(dictionary[key])
                select dictionary[key]).FirstOrDefault<string>();
        }
    }
}
