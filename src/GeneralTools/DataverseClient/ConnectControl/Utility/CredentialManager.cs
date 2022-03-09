using Microsoft.PowerPlatform.Dataverse.ConnectControl.InternalExtensions;
using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Microsoft.PowerPlatform.Dataverse.ConnectControl.Utility
{
	/// <summary>
	/// Represents a credential stored in credential manager
	/// </summary>
	public sealed class SavedCredentials
	{
		/// <summary>
		/// Instantiates an instance of the SavedCredentials class
		/// </summary>
		public SavedCredentials()
		{
		}

		/// <summary>
		/// Instantiates an instance of the SavedCredentials class
		/// </summary>
		public SavedCredentials(string userName, SecureString password)
		{
			if (string.IsNullOrWhiteSpace(userName))
			{
				throw new ArgumentNullException("userName");
			}

			this.UserName = userName;
			this.Password = password;
		}

		/// <summary>
		/// Username
		/// </summary>
		public string UserName { get; set; }

		/// <summary>
		/// Password
		/// </summary>
		public SecureString Password { get; set; }
	}

	/// <summary>
	/// This class exposes methods to read, write and delete user credentials
	/// </summary>
	public static class CredentialManager
	{
		/// <summary>
		/// Target Name against which all credentials are stored on the disk.
		/// </summary>
		public const string CrmTargetPrefix = "MicrosoftCRM_";

		/// <summary>
		/// Retrieves the URI (for a given target URI) that will be present in the saved credentials
		/// </summary>
		/// <param name="target">Target to be read</param>
		public static Uri GetCredentialTarget(Uri target)
		{
			if (null == target)
			{
				throw new ArgumentNullException("target");
			}

			return new Uri(target.GetLeftPart(UriPartial.Authority));
		}

		/// <summary>
		/// Reads the credentials for the given target URI
		/// </summary>
		/// <param name="target">URI for the target</param>
		public static SavedCredentials ReadCredentials(Uri target)
		{
			if (target == null)
			{
				throw new ArgumentNullException("target");
			}

			return ReadCredentials(CrmTargetPrefix + target.ToString());
		}

		/// <summary>
		/// Reads the saved credentials for a given target
		/// </summary>
		/// <param name="target">Target that is persisted</param>
		[SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "Target string is not necessarily a uri")]
		public static SavedCredentials ReadCredentials(string target)
		{
			if (string.IsNullOrWhiteSpace(target))
			{
				throw new ArgumentNullException("target");
			}

			SavedCredentials credentials;
			if (!NativeMethods.CredRead(target, CRED_TYPE.GENERIC, 0, out credentials))
			{
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read the saved credentials.");
			}

			// Deal with null password. 
			if (credentials.Password == null)
				credentials.Password = new SecureString(); // set to empty object. 

			return credentials;
		}

		/// <summary>
		/// Writes the credentials.
		/// </summary>
		/// <param name="target">Target is the key with which associated credentials can be fetched</param>
		/// <param name="userCredentials">It is the in parameter which contains the username and password</param>
		/// <param name="storePasswordInCredentialCache">Indicates that the password should be persisted to the credential cache</param>
		public static void WriteCredentials(Uri target, SavedCredentials userCredentials, bool storePasswordInCredentialCache)
		{
			if (null == target)
			{
				throw new ArgumentNullException("target");
			}

			WriteCredentials(CrmTargetPrefix + target.ToString(), userCredentials, storePasswordInCredentialCache);
		}

		/// <summary>
		/// Writes the credentials.
		/// </summary>
		/// <param name="target">Target is the key with which associated credentials can be fetched</param>
		/// <param name="userCredentials">It is the in parameter which contains the username and password</param>
		/// <param name="storePasswordInCredentialCache">Indicates that the password should be persisted to the credential cache</param>
		[SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "Target string is not necessarily a uri")]
		public static void WriteCredentials(string target, SavedCredentials userCredentials, bool storePasswordInCredentialCache)
		{
			if (userCredentials.Password == null)
				userCredentials.Password = new SecureString();  // Create a new empty secure string password to use for password reset. 

			if (string.IsNullOrWhiteSpace(target))
			{
				throw new ArgumentNullException("target");
			}
			else if (null == userCredentials)
			{
				throw new ArgumentNullException("userCredentials");
			}
			else if (string.IsNullOrWhiteSpace(userCredentials.UserName))
			{
				throw new ArgumentNullException("userCredentials", "UserName property has not been set");
			}
			else if (storePasswordInCredentialCache && userCredentials.Password.Length < 0)
			{
				throw new ArgumentNullException("userCredentials",
					"When storePasswordInCredentialCache is true, the Password property must be set.");
			}

			CREDENTIAL_STRUCT credential = new CREDENTIAL_STRUCT();
			try
			{
				credential.targetName = target;
				credential.type = (UInt32)CRED_TYPE.GENERIC;
				credential.userName = userCredentials.UserName;
				credential.attributeCount = 0;
				credential.persist = (UInt32)CRED_PERSIST.LOCAL_MACHINE;

				byte[] passwordBytes = Encoding.Unicode.GetBytes(storePasswordInCredentialCache ? userCredentials.Password.ToUnsecureString() : string.Empty);
				credential.credentialBlobSize = (UInt32)passwordBytes.Length;
				credential.credentialBlob = Marshal.AllocCoTaskMem(passwordBytes.Length);
				Marshal.Copy(passwordBytes, 0, credential.credentialBlob, passwordBytes.Length);
				if (!NativeMethods.CredWrite(ref credential, 0))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to write the saved credentials.");
				}
			}
			finally
			{
				if (IntPtr.Zero != credential.credentialBlob)
				{
					Marshal.FreeCoTaskMem(credential.credentialBlob);
				}
			}
		}

		/// <summary>
		/// Deletes the credentials.
		/// </summary>
		/// <param name="target">Target is the key with which associated credentials can be fetched</param>
		/// <param name="deletePasswordOnly">Indicates that the password should be overwritten, but the 
		/// credential should not be removed from the credential cache.</param>
		public static void DeleteCredentials(Uri target, bool deletePasswordOnly)
		{
			if (null == target)
			{
				throw new ArgumentNullException("target");
			}

			DeleteCredentials(CrmTargetPrefix + target.ToString(), deletePasswordOnly);
		}

		/// <summary>
		/// Deletes the credentials.
		/// </summary>
		/// <param name="target">Target is the key with which associated credentials can be fetched</param>
		/// <param name="deletePasswordOnly">Indicates that the password should be overwritten, but the 
		/// credential should not be removed from the credential cache.</param>
		[SuppressMessage("Microsoft.Design", "CA1057:StringUriOverloadsCallSystemUriOverloads", Justification = "Target string is not necessarily a uri")]
		public static void DeleteCredentials(string target, bool deletePasswordOnly)
		{
			if (string.IsNullOrWhiteSpace(target))
			{
				throw new ArgumentNullException("target");
			}

			if (deletePasswordOnly)
			{
				WriteCredentials(target, new SavedCredentials(ReadCredentials(target).UserName, null), true);
			}
			else
			{
				if (!NativeMethods.CredDelete(target, (int)CRED_TYPE.GENERIC, 0))
				{
					throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to delete the saved credentials.");
				}
			}
		}

		#region Native Methods
		/// <summary>
		/// This structure maps to the CREDENTIAL structure used by native code. We can use this to marshal our values.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		[SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable", Justification = "The Memory management is handled manually")]
		private struct CREDENTIAL_STRUCT
		{
			public UInt32 flags;
			public UInt32 type;
			public string targetName;
			public string comment;
			public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
			public UInt32 credentialBlobSize;
			[SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources", Justification = "The cleanup of credentialBlob is handled manually")]
			public IntPtr credentialBlob;
			public UInt32 persist;
			public UInt32 attributeCount;
			public IntPtr credAttribute;
			public string targetAlias;
			public string userName;
		}

		private enum CRED_TYPE : int
		{
			GENERIC = 1,
			DOMAIN_PASSWORD = 2,
			DOMAIN_CERTIFICATE = 3,
			DOMAIN_VISIBLE_PASSWORD = 4,
			MAXIMUM = 5
		}

		private enum CRED_PERSIST : uint
		{
			SESSION = 1,
			LOCAL_MACHINE = 2,
			ENTERPRISE = 3
		}

		private static class NativeMethods
		{
			[DllImport("advapi32.dll", SetLastError = true, EntryPoint = "CredReadW", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, [MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(CredentialMarshaler))] out SavedCredentials credential);

			[DllImport("Advapi32.dll", SetLastError = true, EntryPoint = "CredWriteW", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CredWrite(ref CREDENTIAL_STRUCT credential, UInt32 flags);

			[DllImport("Advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CredFree(IntPtr cred);

			[DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
			[return: MarshalAs(UnmanagedType.Bool)]
			public static extern bool CredDelete(string target, int type, int flags);
		}

		[SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "The class is instantiated with a call to ReadCredentials")]
		private sealed class CredentialMarshaler : ICustomMarshaler
		{
			private static ICustomMarshaler _instance;

			void ICustomMarshaler.CleanUpManagedData(object ManagedObj)
			{
				// Nothing to do since all data can be garbage collected.
			}

			void ICustomMarshaler.CleanUpNativeData(IntPtr pNativeData)
			{
				if (IntPtr.Zero == pNativeData)
				{
					return;
				}

				NativeMethods.CredFree(pNativeData);
			}

			int ICustomMarshaler.GetNativeDataSize()
			{
				throw new NotImplementedException();
			}

			IntPtr ICustomMarshaler.MarshalManagedToNative(object obj)
			{
				throw new NotImplementedException();
			}

			object ICustomMarshaler.MarshalNativeToManaged(IntPtr pNativeData)
			{
				if (IntPtr.Zero == pNativeData)
				{
					return null;
				}

				CREDENTIAL_STRUCT credentials = (CREDENTIAL_STRUCT)Marshal.PtrToStructure(pNativeData, typeof(CREDENTIAL_STRUCT));
				string userName = credentials.userName;

				//The password may not be persisted to the credential cache. If it is, it's size will be greater than 0 (and it will
				//be persisted into the credential cache).
				string password;
				int passwordLength = (int)credentials.credentialBlobSize;
				if (0 == passwordLength)
				{
					password = string.Empty;
				}
				else
				{
					byte[] passwordBytes = new byte[passwordLength];
					Marshal.Copy(credentials.credentialBlob, passwordBytes, 0, passwordLength);

					password = Encoding.Unicode.GetString(passwordBytes);
				}

				SecureString ps = new SecureString();
				return new SavedCredentials(userName, ps.MakeSecureString(password));
			}

			[SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Code is called via reflection during marshalling of the object.")]
			[SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "cookie", Justification = "The String parameter is a neceesary parameter for the ICustomMarshaler interface.")]
			public static ICustomMarshaler GetInstance(string cookie)
			{
				if (null == _instance)
				{
					_instance = new CredentialMarshaler();
				}

				return _instance;
			}
		}
		#endregion
	}

	#region Extension Methods for SecureString
	/// <summary>
	/// Adds a extension to Secure string
	/// </summary>
	internal static class SecureStringExtensions
	{
		/// <summary>
		///  Makes a secure string 
		/// </summary>
		/// <param name="value"></param>
		/// <param name="pass"></param>
		/// <returns></returns>
		public static SecureString MakeSecureString(this SecureString value, string pass)
		{
			value.Clear(); 
			if (!string.IsNullOrEmpty(pass))
			{
				foreach (char c in pass)
				{
					value.AppendChar(c);
				}
				value.MakeReadOnly(); // Lock it down. 
				return value;
			}
			return null;
		}
	}

	#endregion
}