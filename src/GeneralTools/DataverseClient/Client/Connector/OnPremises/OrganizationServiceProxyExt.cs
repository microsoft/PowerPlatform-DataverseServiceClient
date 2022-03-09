using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using Microsoft.Xrm.Sdk.Client;

namespace Microsoft.PowerPlatform.Dataverse.Client.Connector.OnPremises
{
    /// <summary>
    /// Extension to the Organization Service Proxy to allow for management of reAuthentication
    /// Base class borrowed from the Plugin Registration tool, and modified for this class.
    /// </summary>
    internal sealed class ManagedTokenOrganizationServiceProxy : OrganizationServiceProxyAsync
    {
        /// <summary>
        /// Support for AD
        /// </summary>
        /// <param name="serviceManagement"></param>
        /// <param name="clientCredentials"></param>
        public ManagedTokenOrganizationServiceProxy(IServiceManagement<IOrganizationServiceAsync> serviceManagement, ClientCredentials clientCredentials)
            : base(serviceManagement, clientCredentials)
        {

        }

        /// <summary>
        ///  Support for things other then AD.
        /// </summary>
        /// <param name="serviceManagement"></param>
        /// <param name="securityTokenResponse"></param>
        /// <param name="clientCredentials"></param>
        public ManagedTokenOrganizationServiceProxy(IServiceManagement<IOrganizationServiceAsync> serviceManagement, SecurityTokenResponse securityTokenResponse, ClientCredentials clientCredentials)
            : base(serviceManagement, clientCredentials)
        {
            // While this process is odd, it is functional and allows for the onboard ReAuthenticate system to work correctly
            this.SecurityTokenResponse = securityTokenResponse;
        }

        // <summary>
        // Called when the device needs to be r
        // </summary>
        // <returns></returns>
        //protected override SecurityTokenResponse AuthenticateDeviceCore()
        //{
        //	if (_deviceCredentials == null)
        //	{
        //		_deviceCredentials = DeviceIdManager.LoadOrRegisterDevice(
        //			this.ServiceConfiguration.CurrentIssuer.IssuerAddress.Uri);
        //	}
        //	return ServiceConfiguration.AuthenticateDevice(this._deviceCredentials);
        //}

        /// <summary>
        /// Overrides the Authentication core process in the SDK Proxy..
        /// </summary>
        protected override void AuthenticateCore()
        {
            base.AuthenticateCore();
        }

        /// <summary>
        /// Determines if a ReAuthentication is required for this call.
        /// </summary>
        protected override void ValidateAuthentication()
        {
#if NETFRAMEWORK
            if (SecurityTokenResponse != null &&
               DateTime.UtcNow >= SecurityTokenResponse.Response.Lifetime.Expires)
#else
			if (SecurityTokenResponse != null &&
			   DateTime.UtcNow >= SecurityTokenResponse.Token.ValidTo)
#endif
            {
                try
                {
                    Authenticate();
                }
                catch (CommunicationException)
                {
                    throw;
                }
            }
            base.ValidateAuthentication();
        }
    }
}
