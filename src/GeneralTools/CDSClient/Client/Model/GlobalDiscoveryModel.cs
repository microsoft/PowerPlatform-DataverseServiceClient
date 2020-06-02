using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client.Model
{
	internal class GlobalDiscoveryModel
	{
		[JsonProperty(PropertyName = "@odata.context")]
		public string context { get; set; }
		[JsonProperty(PropertyName = "value")]
		public IList<GlobalDiscoveryInstanceModel> Instances { get; set; }
	}

	internal class GlobalDiscoveryInstanceModel
	{
		[JsonProperty(PropertyName = "Id")]
		public Guid Id { get; set; }
		[JsonProperty(PropertyName = "UniqueName")]
		public string UniqueName { get; set; }
		[JsonProperty(PropertyName = "UrlName")]
		public string UrlName { get; set; }
		[JsonProperty(PropertyName = "FriendlyName")]
		public string FriendlyName { get; set; }
		[JsonProperty(PropertyName = "State")]
		public int State { get; set; }
		[JsonProperty(PropertyName = "Version")]
		public string Version { get; set; }
		[JsonProperty(PropertyName = "Url")]
		public string Url { get; set; }
		[JsonProperty(PropertyName = "ApiUrl")]
		public string ApiUrl { get; set; }
		[JsonProperty(PropertyName = "LastUpdated")]
		public string LastUpdated { get; set; }
		[JsonProperty(PropertyName = "Region")]
		public string Region { get; set; }
		[JsonProperty(PropertyName = "OrganizationType")]
		public string OrganizationType { get; set; }
		[JsonProperty(PropertyName = "TenantId")]
		public string TenantId { get; set; }
		[JsonProperty(PropertyName = "EnvironmentId")]
		public string EnvironmentId { get; set; }
		[JsonProperty(PropertyName = "StatusMessage")]
		public string StatusMessage { get; set; }
		[JsonProperty(PropertyName = "TrialExpirationDate")]
		public DateTime TrialExpirationDate { get; set; }
		[JsonProperty(PropertyName = "Purpose")]
		public string Purpose { get; set; }


	}
}
