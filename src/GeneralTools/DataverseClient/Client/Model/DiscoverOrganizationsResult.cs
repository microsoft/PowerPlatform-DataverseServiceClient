#region using
using Microsoft.Identity.Client;
using Microsoft.Xrm.Sdk.Discovery;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client.Model
{
    /// <summary>
    /// Result of call to DiscoverOrganizationsAsync
    /// </summary>
    public class DiscoverOrganizationsResult
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="organizationDetailCollection">OrganizationDetailCollection</param>
        /// <param name="account">account</param>
        public DiscoverOrganizationsResult(OrganizationDetailCollection organizationDetailCollection, IAccount account)
        {
            OrganizationDetailCollection = organizationDetailCollection;
            Account = account;
        }

        /// <summary>
        /// OrganizationDetailCollection
        /// </summary>
        public OrganizationDetailCollection OrganizationDetailCollection { get; private set; }

        /// <summary>
        /// MSAL account selected as part of dicovery authentication
        /// </summary>
        public IAccount Account { get; private set; }
    }
}
