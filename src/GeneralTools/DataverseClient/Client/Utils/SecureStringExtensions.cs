using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	/// <summary>
	/// Adds a extension to Secure string
	/// </summary>
	internal static class SecureStringExtensions
	{
		/// <summary>
		/// DeCrypt a Secure password 
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static string ToUnsecureString(this SecureString value)
		{
			if (null == value)
				throw new ArgumentNullException("value");

			// Get a pointer to the secure string memory data. 
			IntPtr ptr = Marshal.SecureStringToGlobalAllocUnicode(value);
			try
			{
				// DeCrypt
				return Marshal.PtrToStringUni(ptr);
			}
			finally
			{
				// release the pointer. 
				Marshal.ZeroFreeGlobalAllocUnicode(ptr);
			}
		}
	}
}
