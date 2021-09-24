using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
	/// <summary>
	/// Interface containing extension methods provided by the DataverseServiceClient for the IOrganizationService Interface.
	/// These extensions will only operate from within the client and are not supported server side.
	/// </summary>
	public interface IOrganizationServiceAsync2
	{
		/// <summary>
		/// Create an entity and process any related entities
		/// </summary>
		/// <param name="entity">entity to create</param>
		/// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
		/// <returns>Returns the newly created record</returns>
		Task<Entity> CreateAndReturnAsync(Entity entity, CancellationToken cancellationToken);

	}
}
