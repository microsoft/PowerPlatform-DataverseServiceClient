using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Reflection;
using Microsoft.PowerPlatform.Dataverse.ConnectControl.Properties;
using System.Security.Cryptography;
using System.Xml.Serialization;
using System.Globalization;


namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
	/// <summary>
	/// Provides utility services for accessing, loading and describing configuration entries used by the CrmConnect classes.
	/// </summary>
	public class StorageUtils
	{
		/// <summary>
		/// Gets a key from the Server config Keys 
		/// </summary>
		/// <param name="ServerConfigKeys"></param>
		/// <param name="key">Key to get</param>
		/// <returns></returns>
		public static T GetConfigKey<T>(Dictionary<Dynamics_ConfigFileServerKeys, object> ServerConfigKeys, Dynamics_ConfigFileServerKeys key)
		{
			try
			{
				if (ServerConfigKeys.ContainsKey(key))
				{
					return (T)ServerConfigKeys[key];
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine(string.Format("{0} , {1}", ex.GetType().ToString(), ex.Message));				
			}
			return default(T);
		}

		/// <summary>
		/// Set a key in the server config keys list. 
		/// </summary>
		/// <param name="ServerConfigKeys"></param>
		/// <param name="key"></param>
		/// <param name="value"></param>
		public static void SetConfigKey<T>(Dictionary<Dynamics_ConfigFileServerKeys, object> ServerConfigKeys, Dynamics_ConfigFileServerKeys key, T value)
		{
			try
			{
				if (ServerConfigKeys.ContainsKey(key))
				{
					ServerConfigKeys[key] = value;
				}
				else
					ServerConfigKeys.Add(key, value);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine(string.Format("{0} , {1}", ex.GetType().ToString(), ex.Message));
			}
		}

		/// <summary>
		/// Reads the HomeRealmsStore.xml file and loads the results into an HomeRealmOptions class.
		/// </summary>
		/// <remarks>Populated HomeRealmOptionsObject or Null</remarks>
		public static Model.ClaimsHomeRealmOptions ReadHomeRealmConfigFile()
		{
			try
			{
				Assembly asm = Assembly.GetExecutingAssembly();
				string ConfigPath = Path.Combine(Path.GetDirectoryName(asm.Location), Resources.LOGIN_FRM_AUTHTYPE_CONFIG_FILE_NAME);

				// check for the config file
				if (File.Exists(ConfigPath))
				{
					using (StreamReader rd = new StreamReader(ConfigPath))
					{
						Model.ClaimsHomeRealmOptions HomeRealmOps = Deserialize<Model.ClaimsHomeRealmOptions>(rd.ReadToEnd());
						return HomeRealmOps;
					}
				}
				else
					return null;
			}
			catch (DirectoryNotFoundException dEx)
			{
				ErrorLogger.WriteToFile(dEx);
			}
			catch (IOException iOEx)
			{
				ErrorLogger.WriteToFile(iOEx);
			}
			catch (Exception ex)
			{
				ErrorLogger.WriteToFile(ex);
			}
			return null;
		}


		#region utilitysforthiscontrol

		/// <summary>
		/// Deserialize a string to a object
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <param name="deserializeString"></param>
		/// <returns></returns>
		private static TType Deserialize<TType>(string deserializeString)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(TType));
			using (StringReader reader = new StringReader(deserializeString))
			{
				return (TType)serializer.Deserialize(reader);
			}
		}

		/// <summary>
		/// Deserialize a string to a object
		/// </summary>
		/// <param name="deserializeString"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		private static object Deserialize(string deserializeString, Type type)
		{
			XmlSerializer serializer = new XmlSerializer(type);
			using (StringReader reader = new StringReader(deserializeString))
			{
				return serializer.Deserialize(reader);
			}
		}


		/// <summary>
		/// Serialize a Type to a string
		/// </summary>
		/// <typeparam name="TType"></typeparam>
		/// <param name="serializeObject"></param>
		/// <returns></returns>
		private static string Serialize<TType>(TType serializeObject)
		{
			using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
			{
				new XmlSerializer(typeof(TType)).Serialize((TextWriter)writer, serializeObject);
				return writer.ToString();
			}
		}

		/// <summary>
		/// Serialize a Type to a string
		/// </summary>
		/// <param name="serialize"></param>
		/// <returns></returns>
		private static string Serialize(object serialize)
		{
			using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
			{
				new XmlSerializer(serialize.GetType()).Serialize((TextWriter)writer, serialize);
				return writer.ToString();
			}
		}


		#endregion
	}

	/// <summary>
	/// Enum for CrmConnection Configuration Keys in the app.config. 
	/// </summary>
	public enum Dynamics_ConfigFileServerKeys
	{
		/// <summary>
		/// Use Default Credentials. 
		/// </summary>
		UseDefaultCreds = 1,
		/// <summary>
		/// User name to connect to CRM with 
		/// </summary>
		CrmUserName,
		/// <summary>
		/// Password used when connecting to CRM
		/// </summary>
		CrmPassword,
		/// <summary>
		/// Domain used when connecting to CRM
		/// </summary>
		CrmDomain,
		/// <summary>
		/// CRM Org Connecting too.
		/// </summary>
		CrmOrg,
		/// <summary>
		/// CRM Server name
		/// </summary>
		CrmServerName,
		/// <summary>
		/// Port CRM server is listening on 
		/// </summary>
		CrmPort,
		/// <summary>
		/// Type of CRM deployment used indicating where CRM is running
		/// used to be UiiCrmUseOnPrem
		/// </summary>
		CrmDeploymentType,
		/// <summary>
		/// Flag, encrypt and hold creds.
		/// </summary>
		CacheCredentials,
		/// <summary>
		/// Use SSL ?
		/// </summary>
		CrmUseSSL,
		/// <summary>
		/// What CRM online Region to use
		/// </summary>
		CrmOnlineRegion,
		/// <summary>
		/// what Home Realm to use
		/// </summary>
		AuthHomeRealm,
		/// <summary>
		/// Use the system ask for the org each login. 
		/// </summary>
		AskForOrg,
		/// <summary>
		/// To Display Advanced option for O365.
		/// </summary>
		AdvancedCheck,
		/// <summary>
		/// Authority for OAuth login
		/// </summary>
		Authority,
		/// <summary>
		/// UserId for O365-OAuth login
		/// </summary>
		UserId,
        /// <summary>
        /// Use Direct Connect URI
        /// </summary>
        UseDirectConnection,
        /// <summary>
        /// Direct connection URI String
        /// </summary>
        DirectConnectionUri
    }

	/// <summary>
	/// This identifies the authentication type used. 
	/// </summary>
	public enum CrmDeploymentType
	{
		/// <summary>
		/// Using Premise 
		/// </summary>
		Prem,
		/// <summary>
		/// Using CRM Online
		/// </summary>
		Online,
		/// <summary>
		/// Using Office365
		/// </summary>
		O365
	}


}
