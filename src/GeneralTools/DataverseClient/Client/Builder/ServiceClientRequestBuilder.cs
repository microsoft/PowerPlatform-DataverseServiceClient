// Ignore Spelling: Dataverse

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client.Builder
{
    /// <summary>
    /// Request builder class for submitting requests to Dataverse.
    /// </summary>
    public class ServiceClientRequestBuilder : AbstractClientRequestBuilder<ServiceClientRequestBuilder>
    {
        internal ServiceClientRequestBuilder(IOrganizationServiceAsync2 client)
            : base(client)
        { }
    }
}
