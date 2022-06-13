using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerPlatform.Dataverse.Client.InternalExtensions;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	internal static class ConnectionStringConstants
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

		public static string CreateConnectionStringFromConnectionOptions(Model.ConnectionOptions options)
        {
			
			if ( options != null)
            {
				StringBuilder sbConnectionString = new StringBuilder();
				if (options.ServiceUri != null)
					sbConnectionString.Append($"ServiceUri={options.ServiceUri};");

				sbConnectionString.Append($"AuthType={options.AuthenticationType};");
				switch (options.AuthenticationType)
                {
                    case AuthenticationType.AD:
						if (options.UseCurrentUserForLogin) sbConnectionString.Append($"Integrated Security=True;");
						sbConnectionString.Append($"UserName={options.UserName};");
						sbConnectionString.Append($"Password={options.Password.ToUnsecureString()};");
						if (!string.IsNullOrEmpty(options.Domain)) sbConnectionString.Append($"Domain={options.Domain};");
						break;
                    case AuthenticationType.OAuth:
						if (options.UseCurrentUserForLogin) sbConnectionString.Append($"Integrated Security=True;");
						if (!string.IsNullOrEmpty(options.UserName)) sbConnectionString.Append($"UserName={options.UserName};");
						if (options.Password != null ) sbConnectionString.Append($"Password={options.Password.ToUnsecureString()};");
						if (!string.IsNullOrEmpty(options.TokenCacheStorePath)) sbConnectionString.Append($"TokenCacheStorePath={options.TokenCacheStorePath};");
						if (options.LoginPrompt.HasValue) sbConnectionString.Append($"LoginPrompt={options.LoginPrompt.Value};");
						sbConnectionString.Append($"ClientId={options.ClientId};");
						if (options.RedirectUri != null) sbConnectionString.Append($"RedirectUri={options.RedirectUri};");
						break;
                    case AuthenticationType.Certificate:
						sbConnectionString.Append($"ClientId={options.ClientId};");
						sbConnectionString.Append($"CertificateThumbprint={options.CertificateThumbprint};");
						sbConnectionString.Append($"CertificateStoreName={options.CertificateStoreName};");
						break;
                    case AuthenticationType.ClientSecret:
						sbConnectionString.Append($"ClientId={options.ClientId};");
						sbConnectionString.Append($"ClientSecret={options.ClientSecret};");
						break;
                    case AuthenticationType.ExternalTokenManagement:
                        break;
                    case AuthenticationType.InvalidConnection:
                        break;
                    default:
                        break;
                }
				if (options.SkipDiscovery) sbConnectionString.Append($"SkipDiscovery={options.SkipDiscovery};");
				sbConnectionString.Append($"RequireNewInstance={options.RequireNewInstance};");
				
				return sbConnectionString.ToString();
			}
			else
				return string.Empty; 
        }
	}
}
