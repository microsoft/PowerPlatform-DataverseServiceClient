using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	static class ConnectionStringConstants
	{
		public static readonly string[] ServiceUri = { "ServiceUri", "Service Uri", "Url", "Server" };
		public static readonly string[] UserName = { "UserName", "User Name", "UserId", "User Id" };
		public static readonly string[] Password = { "Password" };
		public static readonly string[] Domain = { "Domain" };
		public static readonly string[] HomeRealmUri = { "HomeRealmUri", "Home Realm Uri" };
		public static readonly string[] AuthType = { "AuthType", "AuthenticationType" };
		public static readonly string[] RequireNewInstance = { "RequireNewInstance" };
		public static readonly string[] ClientId = { "ClientId", "AppId", "ApplicationId" };
		public static readonly string[] RedirectUri = { "RedirectUri", "ReplyUrl" };
		public static readonly string[] TokenCacheStorePath = { "TokenCacheStorePath" };
		public static readonly string[] LoginPrompt = { "LoginPrompt" };
		public static readonly string[] CertThumbprint = { "CertificateThumbprint", "Thumbprint" };
		public static readonly string[] CertStoreName = { "CertificateStoreName", "StoreName" };
		public static readonly string[] SkipDiscovery = { "SkipDiscovery" };
		public static readonly string[] IntegratedSecurity = { "Integrated Security" };
		public static readonly string[] ClientSecret = { "ClientSecret" , "Secret" };
	}
}
