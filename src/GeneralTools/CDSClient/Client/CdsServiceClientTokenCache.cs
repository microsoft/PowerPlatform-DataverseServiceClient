using System;
using System.Threading.Tasks;
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using Microsoft.Identity.Client;

namespace Microsoft.PowerPlatform.Cds.Client
{
	/// <summary>
	/// Primary implementation of TokenCache
	/// </summary>
	internal class CdsServiceClientTokenCache : IDisposable
	{
		private CdsTraceLogger logEntry;

		private static string _cacheFilePath;

		private object _fileLocker = new object();
		/// <summary>
		/// Flag to control if the protected data API should be used.
		/// this flag needs to be disabled if running under Azure WebApp contexts as they do not provide access to the encryption feature.
		/// </summary>
		private bool _UseLocalFileEncryption = true;

		/// <summary>
		/// Constructor with Parameter cacheFilePath
		/// </summary>
		/// <param name="cacheFilePath"></param>
		/// <param name="tokenCache"></param>
		public CdsServiceClientTokenCache(ITokenCache tokenCache , string cacheFilePath)
		{
			logEntry = new CdsTraceLogger();

			//If cacheFilePath is provided
			if (!string.IsNullOrEmpty(cacheFilePath))
			{
				_cacheFilePath = cacheFilePath;

				// Register MSAL event handlers.
				tokenCache.SetBeforeAccess(BeforeAccessNotification);
				tokenCache.SetAfterAccess(AfterAccessNotification);

				// Need to revist this for adding support for other cache providers: 
				// https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-net-token-cache-serialization 

				//				// Try to encrypt some data to test if Protect is available. 
				//				try
				//				{
				//#pragma warning disable CS0618 // Type or member is obsolete
				//					ProtectedData.Protect(this.Serialize(), null, DataProtectionScope.CurrentUser);
				//#pragma warning restore CS0618 // Type or member is obsolete
				//				}
				//				catch (Exception ex)
				//				{
				//					_UseLocalFileEncryption = false;
				//					logEntry.Log("Encryption System not available in this environment", TraceEventType.Warning, ex);
				//				}


				// Create token cache file if one does not already exist.
				if (!File.Exists(_cacheFilePath))
				{
					string directoryName = Path.GetDirectoryName(_cacheFilePath);

					if (!Directory.Exists(directoryName))
					{
						Directory.CreateDirectory(directoryName);
					}

					//File.Create(string) returns an instance of the FileStream class. You need to use Close() method 
					//in order to close it and release resources which are using
					try
					{
						File.Create(_cacheFilePath).Close();
						// Encrypt the file
						try
						{
							// user is using a specified file directory... encrypt file to user using Machine / FS Locking. 
							// this will lock / prevent users other then the current user from accessing this file. 
							FileInfo fi = new FileInfo(_cacheFilePath);
							if (_UseLocalFileEncryption)
								fi.Encrypt();
						}
						catch (IOException)
						{
							// This can happen when a certificate system on the host has failed. 
							// usually this can be fixed with the steps in this article : http://support.microsoft.com/kb/937536
							//logEntry.Log(string.Format("{0}\r\nException Details : {1}", "Failed to Encrypt Configuration File!", encrEX), TraceEventType.Error);
							//logEntry.Log("This problem may be related to a domain certificate in windows being out of sync with the domain, please read http://support.microsoft.com/kb/937536");
						}
						catch (Exception)
						{
							//logEntry.Log(string.Format("Failed to Encrypt Configuration File!", genEX), TraceEventType.Error);
						}
					}
					catch (Exception exception)
					{
						logEntry.Log(string.Format("{0}\r\nException Details : {1}", "Error occurred in CdsServiceClientTokenCache.CdsServiceClientTokenCache(). ", exception), TraceEventType.Error);
					}
				}

				//// Register ADAL event handlers.
				//this.AfterAccess = AfterAccessNotification;
				//this.BeforeAccess = BeforeAccessNotification;

//				lock (_fileLocker)
//				{
//					try
//					{
//						// Read token from the persistent store and supply it to ADAL's in memory cache.
//						if (_UseLocalFileEncryption)
//#pragma warning disable CS0618 // Type or member is obsolete
//							this.Deserialize(File.Exists(_cacheFilePath) && File.ReadAllBytes(_cacheFilePath).Length != 0
//								? ProtectedData.Unprotect(File.ReadAllBytes(_cacheFilePath), null, DataProtectionScope.CurrentUser)
//								: null);
//#pragma warning restore CS0618 // Type or member is obsolete
//						else
//#pragma warning disable CS0618 // Type or member is obsolete
//							this.Deserialize(File.Exists(_cacheFilePath) && File.ReadAllBytes(_cacheFilePath).Length != 0
//								? File.ReadAllBytes(_cacheFilePath) : null);
//#pragma warning restore CS0618 // Type or member is obsolete
//					}
//					catch (Exception ex)
//					{
//						// Failed to access Local token cache file.. 
//						// Delete it. 
//						logEntry.Log("Failed to access token cache file, resetting the token cache file", TraceEventType.Warning, ex);
//						Clear(_cacheFilePath);
//					}
//				}
			}

		}

		/// <summary>
		/// Empties the persistent and in-memory store.
		/// </summary>
		/// <param name="tokenFilePath"></param>
		/// <returns></returns>
		public bool Clear(string tokenFilePath)
		{
			string deletePath = tokenFilePath;

			//If both parameter and private var is null or empty need to pass false to caller function
			if (string.IsNullOrWhiteSpace(deletePath))
			{
				if (!string.IsNullOrEmpty(_cacheFilePath))
					deletePath = _cacheFilePath;
				else
					return false;
			}

			try
			{
				//clear in-memory cache.
				//base.Clear();
				// check if already exist or not
				if (File.Exists(deletePath))
				{
					// Delete persistent store first for integrity
					File.Delete(deletePath);
				}
			}
			catch (Exception exception)
			{
				logEntry.Log(
					string.Format("{0}\r\nException Details : {1}", "Error occurred in clearing CdsServiceClientTokenCache.Clear(). ",
						exception), TraceEventType.Error);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Triggered right before ADAL needs to access the cache.
		/// Reload the cache from the persistent store in case it changed since the last access.
		/// </summary>
		/// <param name="args"></param>
		private void BeforeAccessNotification(TokenCacheNotificationArgs args)
		{
			if (!args.HasStateChanged) return; // if the token state has not changed... skip this step. 

			lock (_fileLocker)
			{
				// Read token from persistent store and supply it to ADAL's in memory cache.
				if (_UseLocalFileEncryption)
					args.TokenCache.DeserializeMsalV3(File.Exists(_cacheFilePath) && File.ReadAllBytes(_cacheFilePath).Length != 0
					? ProtectedData.Unprotect(File.ReadAllBytes(_cacheFilePath), null, DataProtectionScope.CurrentUser)
					: null);
				else
					args.TokenCache.DeserializeMsalV3(File.Exists(_cacheFilePath) && File.ReadAllBytes(_cacheFilePath).Length != 0
					? File.ReadAllBytes(_cacheFilePath) : null);
			}
		}

		/// <summary>
		/// Triggered right after ADAL accessed the cache.
		/// </summary>
		/// <param name="args"></param>
		private void AfterAccessNotification(TokenCacheNotificationArgs args)
		{
			// If the access operation resulted in a cache update.
			if (!args.HasStateChanged) return;

			lock (_fileLocker)
			{
				try
				{
					// Reflect token changes in the persistent store.
					if (_UseLocalFileEncryption)
						File.WriteAllBytes(_cacheFilePath, ProtectedData.Protect(args.TokenCache.SerializeMsalV3(), null, DataProtectionScope.CurrentUser));
					else
						File.WriteAllBytes(_cacheFilePath, args.TokenCache.SerializeMsalV3());
					// once the write operation took place, restore the HasStateChanged bit to false.
					//args.HasStateChanged = false;
				}
				catch (Exception exception)
				{
					logEntry.Log(string.Format("{0}\r\nException Details : {1}", "Error occurred in CdsServiceClientTokenCache.AfterAccessNotification(). ", exception), TraceEventType.Error);
				}
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		/// <summary>
		/// Cleaning up the object.
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if (logEntry != null)
						logEntry.Dispose();
				}
				disposedValue = true;
			}
		}

		/// <summary>
		/// Clean up the object
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion
	}
}
