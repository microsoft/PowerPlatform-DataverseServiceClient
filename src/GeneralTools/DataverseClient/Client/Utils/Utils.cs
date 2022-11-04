#region using
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.PowerPlatform.Dataverse.Client.Model;
using Microsoft.PowerPlatform.Dataverse.Client.Utils;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// Utility functions the ServiceClient assembly.
    /// </summary>
    internal class Utilities
    {

        internal static DiscoveryServer GetDiscoveryServerByUri(Uri orgUri)
        {
            if (orgUri != null)
            {
                string OnlineRegon = string.Empty;
                string OrgName = string.Empty;
                bool IsOnPrem = false;
                Utilities.GetOrgnameAndOnlineRegionFromServiceUri(orgUri, out OnlineRegon, out OrgName, out IsOnPrem);
                if (!string.IsNullOrEmpty(OnlineRegon))
                {
                    using (DiscoveryServers discoSvcs = new DiscoveryServers())
                    {
                        return discoSvcs.GetServerByShortName(OnlineRegon);
                    };
                }
            }
            return null;
        }

        /// <summary>
        /// Get the organization name and on-line region from the Uri
        /// </summary>
        /// <param name="serviceUri">Service Uri to parse</param>
        /// <param name="isOnPrem">if OnPrem, will be set to true, else false.</param>
        /// <param name="onlineRegion">Name of the Dataverse Online Region serving this request</param>
        /// <param name="organizationName">Name of the Organization extracted from the Service URI</param>
        public static void GetOrgnameAndOnlineRegionFromServiceUri(Uri serviceUri, out string onlineRegion, out string organizationName, out bool isOnPrem)
        {
            isOnPrem = false;
            onlineRegion = string.Empty;
            organizationName = string.Empty;

            //support for detecting a Online URI in the path and rerouting to use that..
            if (IsValidOnlineHost(serviceUri))
            {
                try
                {
                    // Determine deployment region from Uri
                    List<string> elements = new List<string>(serviceUri.Host.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries));
                    organizationName = elements[0];
                    elements.RemoveAt(0); // remove the first ( org name ) from the Uri.


                    // construct Prospective Dataverse Online path.
                    System.Text.StringBuilder buildPath = new System.Text.StringBuilder();
                    foreach (var item in elements)
                    {
                        if (item.Equals("api"))
                            continue; // Skip the .api. when running via this path.
                        buildPath.AppendFormat("{0}.", item);
                    }
                    string dvKey = buildPath.ToString().TrimEnd('.').TrimEnd('/');
                    buildPath.Clear();
                    if (!string.IsNullOrEmpty(dvKey))
                    {
                        using (DiscoveryServers discoSvcs = new DiscoveryServers())
                        {
                            // drop in the discovery region if it can be determined.  if not, default to scanning.
                            var locatedDiscoServer = discoSvcs.OSDPServers.Where(w => w.DiscoveryServerUri != null && w.DiscoveryServerUri.Host.Contains(dvKey)).FirstOrDefault();
                            if (locatedDiscoServer != null && !string.IsNullOrEmpty(locatedDiscoServer.ShortName))
                                onlineRegion = locatedDiscoServer.ShortName;
                        }
                    }
                    isOnPrem = false;
                }
                finally
                { }
            }
            else
            {
                isOnPrem = true;
                //Setting organization for the AD/Onpremise Oauth/IFD
                if (serviceUri.Segments.Count() >= 2)
                {
                    organizationName = serviceUri.Segments[1].TrimEnd('/'); // Fix for bug 294040 http://vstfmbs:8080/tfs/web/wi.aspx?pcguid=12e6d33f-1461-4da4-b3d9-5517a4567489&id=294040
                }else
                {
                    // IFD style. 
                    var segementsList = serviceUri.DnsSafeHost.Split('.');
                    if ( segementsList.Length > 1)
                    {
                        organizationName = segementsList[0];
                    }
                }
            }

        }

        /// <summary>
        /// returns ( if possible ) the org detail for a given organization name from the list of orgs in discovery
        /// </summary>
        /// <param name="orgList">OrgList to Parse though</param>
        /// <param name="organizationName">Name to find</param>
        /// <returns>Found Organization Instance or Null</returns>
        public static OrgByServer DeterminOrgDataFromOrgInfo(OrgList orgList, string organizationName)
        {
            OrgByServer orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.UniqueName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            if (orgDetail == null)
                orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.FriendlyName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

            // still not found... try by URI name.
            if (orgDetail == null)
            {
                string formatedOrgName = string.Format("://{0}.", organizationName).ToLowerInvariant();
                orgDetail = orgList.OrgsList.Where(o => o.OrgDetail.Endpoints[EndpointType.WebApplication].Contains(formatedOrgName)).FirstOrDefault();
            }
            return orgDetail;
        }

        /// <summary>
        /// returns ( if possible ) the org detail for a given organization name from the list of orgs in discovery
        /// </summary>
        /// <param name="orgList">OrgList to Parse though</param>
        /// <param name="organizationName">Name to find</param>
        /// <returns>Found Organization Instance or Null</returns>
        public static OrganizationDetail DeterminOrgDataFromOrgInfo(OrganizationDetailCollection orgList, string organizationName)
        {
            OrganizationDetail orgDetail = orgList.Where(o => o.UniqueName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
            if (orgDetail == null)
                orgDetail = orgList.Where(o => o.FriendlyName.Equals(organizationName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();

            // still not found... try by URI name.
            if (orgDetail == null)
            {
                string formatedOrgName = string.Format("://{0}.", organizationName).ToLowerInvariant();
                orgDetail = orgList.Where(o => o.Endpoints[EndpointType.WebApplication].Contains(formatedOrgName)).FirstOrDefault();
            }
            return orgDetail;
        }

        /// <summary>
        /// Parses an OrgURI to determine what the supporting discovery server is.
        /// </summary>
        /// <param name="serviceUri">Service Uri to parse</param>
        /// <param name="Geo">Geo Code for region (Optional)</param>
        /// <param name="isOnPrem">if OnPrem, will be set to true, else false.</param>
        public static DiscoveryServer DeterminDiscoveryDataFromOrgDetail(Uri serviceUri, out bool isOnPrem, string Geo = null)
        {
            isOnPrem = false;
            //support for detecting a Live/Online URI in the path and rerouting to use that..
            if (IsValidOnlineHost(serviceUri))
            {
                // Check for Geo code and to make sure that the region is not on our internal list.
                if (!string.IsNullOrEmpty(Geo)
                    && !(serviceUri.Host.ToUpperInvariant().Contains("CRMLIVETIE.COM")
                    || serviceUri.Host.ToUpperInvariant().Contains("CRMLIVETODAY.COM"))
                    )
                {
                    using (DiscoveryServers discoSvcs = new DiscoveryServers())
                    {
                        // Find by Geo, if null fall though to next check
                        var locatedDiscoServer = discoSvcs.OSDPServers.Where(w => !string.IsNullOrEmpty(w.GeoCode) && w.GeoCode == Geo).FirstOrDefault();
                        if (locatedDiscoServer != null && !string.IsNullOrEmpty(locatedDiscoServer.ShortName))
                            return locatedDiscoServer;
                    }
                }

                try
                {
                    isOnPrem = false;

                    // Determine deployment region from Uri
                    List<string> elements = new List<string>(serviceUri.Host.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries));
                    elements.RemoveAt(0); // remove the first ( org name ) from the Uri.


                    // construct Prospective Dataverse Online path.
                    System.Text.StringBuilder buildPath = new System.Text.StringBuilder();
                    foreach (var item in elements)
                    {
                        if (item.Equals("api"))
                            continue; // Skip the .api. when running via this path.
                        buildPath.AppendFormat("{0}.", item);
                    }
                    string dvKey = buildPath.ToString().TrimEnd('.').TrimEnd('/');
                    buildPath.Clear();
                    if (!string.IsNullOrEmpty(dvKey))
                    {
                        using (DiscoveryServers discoSvcs = new DiscoveryServers())
                        {
                            // drop in the discovery region if it can be determined.  if not, default to scanning.
                            var locatedDiscoServer = discoSvcs.OSDPServers.Where(w => w.DiscoveryServerUri != null && w.DiscoveryServerUri.Host.Contains(dvKey)).FirstOrDefault();
                            if (locatedDiscoServer != null && !string.IsNullOrEmpty(locatedDiscoServer.ShortName))
                                return locatedDiscoServer;
                        }
                    }
                }
                finally
                { }
            }
            else
            {
                isOnPrem = true;
                return null;
            }
            return null;

        }

        /// <summary>
        /// Looks at the URL provided and determines if the URL is a valid online URI
        /// </summary>
        /// <param name="hostUri">URI to examine</param>
        /// <returns>Returns True if the URI is recognized as online, or false if not.</returns>
        public static bool IsValidOnlineHost(Uri hostUri)
        {
#if DEBUG
            if (hostUri.DnsSafeHost.ToUpperInvariant().Contains("DYNAMICS.COM")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("DYNAMICS-INT.COM")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("MICROSOFTDYNAMICS.DE")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("MICROSOFTDYNAMICS.US")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("APPSPLATFORM.US")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("CRM.DYNAMICS.CN")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("CRMLIVETIE.COM")
                || hostUri.DnsSafeHost.ToUpperInvariant().Contains("CRMLIVETODAY.COM"))
#else
			if (hostUri.DnsSafeHost.ToUpperInvariant().Contains("DYNAMICS.COM")
				|| hostUri.DnsSafeHost.ToUpperInvariant().Contains("MICROSOFTDYNAMICS.DE")
				|| hostUri.DnsSafeHost.ToUpperInvariant().Contains("MICROSOFTDYNAMICS.US")
				|| hostUri.DnsSafeHost.ToUpperInvariant().Contains("APPSPLATFORM.US")
				|| hostUri.DnsSafeHost.ToUpperInvariant().Contains("CRM.DYNAMICS.CN")
				|| hostUri.DnsSafeHost.ToUpperInvariant().Contains("DYNAMICS-INT.COM")) // Allows integration Test as well as PRD
#endif
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if the request type can be translated to WebAPI
        /// This is a temp method to support the staged transition to the webAPI and will be removed or reintegrated with the overall pipeline at some point in the future.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="inLoginFlow"></param>
        /// <returns></returns>
        internal static bool IsRequestValidForTranslationToWebAPI(OrganizationRequest req, bool inLoginFlow = false)
        {
            bool useWebApi = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>().Value.UseWebApi;
            bool useWebApiForLogin = false;
            if (inLoginFlow)
                useWebApiForLogin = ClientServiceProviders.Instance.GetService<IOptions<ConfigurationOptions>>().Value.UseWebApiLoginFlow;

            switch (req.RequestName.ToLowerInvariant())
            {
                case "create":
                case "update":
                case "delete":
                case "importsolution":
                case "exportsolution":
                case "stagesolution":
                    return useWebApi; // Only supported with useWebApi flag
                case "retrievecurrentorganization":
                case "retrieveorganizationinfo":
                case "retrieveversion":
                case "whoami":
                    return useWebApiForLogin; // Separate webAPI login methods from general WebAPI use.
                case "upsert":
                // Disabling WebAPI support for upsert right now due to issues with generating the response.

                // avoid bug in WebAPI around Support for key's as EntityRefeances //TODO: TEMP
                //Xrm.Sdk.Messages.UpsertRequest upsert = (Xrm.Sdk.Messages.UpsertRequest)req;
                //if (upsert.Target.KeyAttributes?.Any(a => a.Value is string) != true)
                //	return false;
                //else
                //return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns Http request method based on request message name
        /// </summary>
        /// <param name="requestName">request name</param>
        /// <returns>Http method</returns>
        internal static HttpMethod RequestNameToHttpVerb(string requestName)
        {
            if (string.IsNullOrWhiteSpace(requestName))
                throw new ArgumentNullException(nameof(requestName));

            switch (requestName.ToLowerInvariant())
            {
                case "retrievecurrentorganization":
                case "retrieveorganizationinfo":
                case "retrieveversion":
                case "retrieveuserlicenseinfo":
                case "whoami":
                    return HttpMethod.Get;
                case "create":
                case "importsolution":
                case "exportsolution":
                case "stagesolution":
                    return HttpMethod.Post;
                case "update":
                case "upsert":
                    return new HttpMethod("Patch");
                case "delete":
                    return HttpMethod.Delete;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Constructs Web API request url and adds public request properties to the url as key/value pairs
        /// </summary>
        internal static string ConstructWebApiRequestUrl(OrganizationRequest request, HttpMethod httpMethod, Entity entity, EntityMetadata entityMetadata)
        {
            var result = new StringBuilder();
            if (httpMethod != HttpMethod.Post)
            {
                if (entity != null)
                {
                    if (entity.KeyAttributes?.Any() == true)
                    {
                        result.Append($"{entityMetadata.EntitySetName}({Utilities.ParseAltKeyCollection(entity.KeyAttributes)})");
                    }
                    else
                    {
                        result.Append($"{entityMetadata.EntitySetName}({entity.Id})");
                    }
                }
                else // Add public properties to Url
                {
                    result.Append(request.RequestName);
                    bool hasProperties = false;

                    object propertyValue;
                    foreach (var property in request.GetType().GetProperties())
                    {
                        if (property.DeclaringType == typeof(OrganizationRequest))
                            continue;

                        propertyValue = property.GetValue(request);
                        if (propertyValue == null)
                            continue;

                        if (!hasProperties)
                        {
                            result.Append("(");
                            hasProperties = true;
                        }
                        else
                        {
                            result.Append(",");
                        }

                        result.Append(property.Name);
                        result.Append("='");
                        result.Append(propertyValue.ToString());
                        result.Append("'");
                    }

                    if (hasProperties)
                    {
                        result.Append(")");
                    }
                }
            }
            else
            {
                if (entityMetadata != null)
                {
                    result.Append(entityMetadata.EntitySetName);
                }
                else
                {
                    result.Append(request.RequestName);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// retry request
        /// </summary>
        /// <param name="req">request</param>
        /// <param name="requestTrackingId">requestTrackingId</param>
        /// <param name="LockWait">LockWait</param>
        /// <param name="logDt">logDt</param>
        /// <param name="logEntry">Dataverse TraceLogger</param>
        /// <param name="sessionTrackingId">sessionTrackingId</param>
        /// <param name="disableConnectionLocking">disableConnectionLocking</param>
        /// <param name="retryPauseTimeRunning">retryPauseTimeRunning</param>
        /// <param name="ex">ex</param>
        /// <param name="errorStringCheck">errorStringCheck</param>
        /// <param name="retryCount">retryCount</param>
        /// <param name="isThrottled">when set indicated this was caused by a Throttle</param>
        /// <param name="webUriReq"></param>
        internal static void RetryRequest(OrganizationRequest req, Guid requestTrackingId, TimeSpan LockWait, Stopwatch logDt,
            DataverseTraceLogger logEntry, Guid? sessionTrackingId, bool disableConnectionLocking, TimeSpan retryPauseTimeRunning,
            Exception ex, string errorStringCheck, ref int retryCount, bool isThrottled, string webUriReq = "")
        {
            retryCount++;
            logEntry.LogFailure(req, requestTrackingId, sessionTrackingId, disableConnectionLocking, LockWait, logDt, ex, errorStringCheck, webUriMessageReq: webUriReq);
            logEntry.LogRetry(retryCount, req, retryPauseTimeRunning, isThrottled: isThrottled);
            System.Threading.Thread.Sleep(retryPauseTimeRunning);
        }

        /// <summary>
        /// Parses an attribute array into a object that can be used to create a JSON request.
        /// </summary>
        /// <param name="sourceEntity">Entity to process</param>
        /// <param name="mUtil">Metadata interface utility</param>
        /// <param name="requestedMethod">Operation being executed</param>
        /// <param name="logger">Log sink</param>
        /// <returns>ExpandoObject</returns>
        internal static ExpandoObject ToExpandoObject(Entity sourceEntity, MetadataUtility mUtil, HttpMethod requestedMethod, DataverseTraceLogger logger )
        {
            dynamic expando = new ExpandoObject();

            // Check for primary Id info:
            if (sourceEntity.Id != Guid.Empty)
                sourceEntity = UpdateEntityAttributesForPrimaryId(sourceEntity, mUtil);

            AttributeCollection entityAttributes = sourceEntity.Attributes;
            if (!(entityAttributes != null) && (entityAttributes.Count > 0))
            {
                return expando;
            }

            var expandoObject = (IDictionary<string, object>)expando;
            var attributes = entityAttributes.ToArray();

            // this is used to support ActivityParties collections
            List<ExpandoObject> partiesCollection = null;

            foreach (var attrib in entityAttributes)
            {
                var keyValuePair = attrib;
                var value = keyValuePair.Value;
                var key = keyValuePair.Key;

                if (value is EntityReference entityReference)
                {
                    var attributeInfo = mUtil.GetAttributeMetadata(sourceEntity.LogicalName, key.ToLower());

                    if (!IsAttributeValidForOperation(attributeInfo, requestedMethod))
                        continue;

                    // Get Lookup attribute meta data for the ER to check for polymorphic relationship.
                    if (attributeInfo is LookupAttributeMetadata attribData)
                    {
                        // Now get relationship to make sure we use the correct name.
                        EntityMetadata eData = mUtil.GetEntityMetadata(EntityFilters.Relationships, sourceEntity.LogicalName);
                        string ERNavName = eData.ManyToOneRelationships.FirstOrDefault(w => w.ReferencingAttribute.Equals(attribData.LogicalName) &&
                                                                                w.ReferencedEntity.Equals(entityReference.LogicalName))
                                                                                ?.ReferencingEntityNavigationPropertyName;
                        if (string.IsNullOrEmpty(ERNavName ))
                        {
                            ERNavName = eData.ManyToOneRelationships.FirstOrDefault(w => w.ReferencingAttribute.Equals(attribData.LogicalName))?.ReferencingEntityNavigationPropertyName;
                        }

                        if (!string.IsNullOrEmpty(ERNavName))
                        {
                            key = ERNavName;
                        }
                        else
                        {
                            logger.Log($"{key} describes an entity reference but does not have a corresponding relationship. Skipping adding it in the {requestedMethod} operation");
                            continue;
                        }

                        // Populate Key property
                        key = $"{key}@odata.bind";
                    }
                    else if (attributeInfo == null)
                    {
                        // Fault here.
                        throw new DataverseOperationException($"Entity Reference {key.ToLower()} was not found for entity {sourceEntity.LogicalName}.", null);
                    }

                    string entityReferanceValue = string.Empty;
                    // process ER Value
                    if (entityReference.KeyAttributes?.Any() == true)
                    {
                        entityReferanceValue = ParseAltKeyCollection(entityReference.KeyAttributes);
                    }
                    else
                    {
                        entityReferanceValue = entityReference.Id.ToString();
                    }


                    value = $"/{mUtil.GetEntityMetadata(EntityFilters.Entity, entityReference.LogicalName).EntitySetName}({entityReferanceValue})";
                }
                else
                {
                    if (value is EntityCollection || value is Entity[])
                    {
                        if (value is Entity[] v1s)
                        {
                            EntityCollection ec = new EntityCollection(((Entity[])value).ToList<Entity>());
                            value = ec;
                        }

                        // try to get the participation type id from the key.
                        int PartyTypeId = PartyListHelper.GetParticipationtypeMasks(key);
                        bool isActivityParty = PartyTypeId != -1;  // if the partytypeID is -1 this is not a activity party collection.

                        if (isActivityParty && partiesCollection == null)
                            partiesCollection = new List<ExpandoObject>(); // Only build it when needed.

                        // build linked collection here.
                        foreach (var ent in (value as EntityCollection).Entities)
                        {
                            ExpandoObject rslt = ToExpandoObject(ent, mUtil, requestedMethod , logger);
                            if (isActivityParty)
                            {
                                var tempDict = ((IDictionary<string, object>)rslt);
                                if (!tempDict.ContainsKey("participationtypemask"))
                                    tempDict.Add("participationtypemask", PartyTypeId);
                                partiesCollection.Add((ExpandoObject)tempDict);
                            }
                        }
                        if (isActivityParty)
                            continue;

                        // Note.. if this is not an activity party but instead an embedded entity.. this will fall though and fail with trying to embed an entity.
                    }
                    else
                    {
                        key = key.ToLower();
                        if (value is OptionSetValueCollection optionSetValues)
                        {
                            string mselectValueString = string.Empty;
                            foreach (var opt in optionSetValues)
                            {
                                mselectValueString += $"{opt.Value},";
                            }
                            if (!string.IsNullOrEmpty(mselectValueString) && mselectValueString.Last().Equals(','))
                                value = mselectValueString.Remove(mselectValueString.Length - 1);
                            else
                                value = null;
                        }
                        else if (value is OptionSetValue optionSetValue)
                        {
                            value = optionSetValue.Value.ToString();
                        }
                        else if (value is DateTime dateTimeValue)
                        {
                            var attributeInfo = mUtil.GetAttributeMetadata(sourceEntity.LogicalName, key.ToLower());
                            if (attributeInfo is DateTimeAttributeMetadata attribDateTimeData)
                            {
                                if (attribDateTimeData.DateTimeBehavior == DateTimeBehavior.DateOnly)
                                {
                                    value = dateTimeValue.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                                }
                                else
                                {
                                    if (attribDateTimeData.DateTimeBehavior == DateTimeBehavior.TimeZoneIndependent)
                                    {
                                        value = dateTimeValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                                    }
                                    else
                                    {
                                        if (attribDateTimeData.DateTimeBehavior == DateTimeBehavior.UserLocal)
                                        {
                                            value = dateTimeValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
                                        }
                                    }
                                }
                            }
                        }
                        else if (value is Money moneyValue)
                        {
                            value = moneyValue.Value;
                        }
                        else if (value is bool boolValue)
                        {
                            value = boolValue.ToString();
                        }
                        else if (value is Guid guidValue)
                        {
                            value = guidValue.ToString();
                        }
                        else if (value is null)
                        {
                            var attributeInfo = mUtil.GetAttributeMetadata(sourceEntity.LogicalName, key.ToLower());

                            if (!IsAttributeValidForOperation((AttributeMetadata)attributeInfo, requestedMethod))
                                continue;

                            if (attributeInfo is Xrm.Sdk.Metadata.LookupAttributeMetadata attribData)
                            {
                                // This will not work for Polymorphic currently.
                                var eData = mUtil.GetEntityMetadata(EntityFilters.Relationships, sourceEntity.LogicalName);
                                var ERNavName = eData.ManyToOneRelationships.FirstOrDefault(w => w.ReferencingAttribute.Equals(attribData.LogicalName) &&
                                                                               w.ReferencedEntity.Equals(attribData.Targets.FirstOrDefault()))
                                                                               ?.ReferencingEntityNavigationPropertyName;
                                if (!string.IsNullOrEmpty(ERNavName))
                                    key = ERNavName;

                                // Populate Key property
                                key = $"{key}@odata.bind";
                            }
                            value = null;
                        }
                    }
                }
                expandoObject.Add(key, value);
            }

            // Check to see if this contained an activity party
            if (partiesCollection?.Count > 0)
            {
                var sourceMdata = mUtil.GetEntityMetadata(sourceEntity.LogicalName);
                if (sourceMdata != null )
                    expandoObject.Add($"{sourceMdata.LogicalName}_activity_parties", partiesCollection);
            }

            return (ExpandoObject)expandoObject;
        }

        /// <summary>
        /// Checks if the operation being preformed is permitted for the attribute.
        /// </summary>
        /// <param name="attrib"></param>
        /// <param name="requestedMethod"></param>
        /// <returns></returns>
        private static bool IsAttributeValidForOperation(AttributeMetadata attrib, HttpMethod requestedMethod)
        {
            switch (requestedMethod.ToString().ToLowerInvariant())
            {
                case "post":
                case "put":
                    if (attrib.IsValidForCreate.HasValue && !attrib.IsValidForCreate.Value)
                        return false;
                    break;
                case "patch":
                    if (attrib.IsValidForUpdate.HasValue && !attrib.IsValidForUpdate.Value)
                        return false;
                    break;
                default:
                    break;
            }
            return true;
        }

        /// <summary>
        /// checks to see if an attribute has been added to the collection containing the ID of the entity .
        /// this is required for the WebAPI to properly function.
        /// </summary>
        /// <param name="sourceEntity"></param>
        /// <param name="mUtil"></param>
        /// <returns></returns>
        private static Entity UpdateEntityAttributesForPrimaryId(Entity sourceEntity, MetadataUtility mUtil)
        {
            if (sourceEntity.Id != Guid.Empty)
            {
                var entMeta = mUtil.GetEntityMetadata(sourceEntity.LogicalName);
                sourceEntity.Attributes[entMeta.PrimaryIdAttribute] = sourceEntity.Id;
            }
            return sourceEntity;
        }

        /// <summary>
        /// Handle general related entity collection construction
        /// </summary>
        /// <param name="rootExpando">Object being added too</param>
        /// <param name="entityName">parent entity</param>
        /// <param name="entityCollection">collection of relationships</param>
        /// <param name="mUtil">meta-data utility</param>
        /// <param name="requestedMethod">Operation being executed</param>
        /// <param name="logger">Logger</param>
        /// <returns></returns>
        internal static ExpandoObject ReleatedEntitiesToExpandoObject(ExpandoObject rootExpando, string entityName, RelatedEntityCollection entityCollection, MetadataUtility mUtil, HttpMethod requestedMethod, DataverseTraceLogger logger)
        {
            if (rootExpando == null)
                return rootExpando;

            if (entityCollection != null && entityCollection.Count == 0)
            {
                // nothing to do, just return.
                return rootExpando;
            }

            foreach (var entItem in entityCollection)
            {
                string key = "";
                bool isArrayRequired = false;
                dynamic expando = new ExpandoObject();
                var expandoObject = (IDictionary<string, object>)expando;
                ExpandoObject childEntities = new ExpandoObject();

                List<ExpandoObject> childCollection = new List<ExpandoObject>();

                // Get the Entity relationship key and entity and reverse it back to the entity key name
                var eData = mUtil.GetEntityMetadata(EntityFilters.Relationships, entItem.Value.Entities[0].LogicalName);

                key = ExtractKeyNameFromRelationship(entItem.Key.SchemaName.ToLower(), entityName, ref isArrayRequired, eData);

                if (string.IsNullOrEmpty(key)) // Failed to find key
                {
                    throw new DataverseOperationException($"Relationship key {entItem.Key.SchemaName} cannot be found for related entities of {entityName}.");
                }

                foreach (var ent in entItem.Value.Entities)
                {
                    // Check to see if the entity itself has related entities
                    if (ent.RelatedEntities != null && ent.RelatedEntities.Count > 0)
                    {
                        childEntities = ReleatedEntitiesToExpandoObject(childEntities, entityName, ent.RelatedEntities, mUtil, requestedMethod, logger);
                    }

                    // generate object.
                    ExpandoObject ent1 = ToExpandoObject(ent, mUtil, requestedMethod, logger);

                    if (((IDictionary<string, object>)childEntities).Count() > 0)
                    {
                        foreach (var item in childEntities)
                        {
                            ((IDictionary<string, object>)ent1).Add(item.Key, item.Value);
                        }
                    }
                    childCollection?.Add(ent1);
                }
                if (childCollection.Count == 1 && isArrayRequired == false)
                    ((IDictionary<string, object>)rootExpando).Add(key, childCollection[0]);
                else
                    ((IDictionary<string, object>)rootExpando).Add(key, childCollection);
            }
            return rootExpando;
        }


        /// <summary>
        /// Helper to extract key name from one of the relationships.
        /// </summary>
        /// <param name="schemaName"></param>
        /// <param name="entityName"></param>
        /// <param name="isArrayRequired"></param>
        /// <param name="eData"></param>
        /// <returns></returns>
        private static string ExtractKeyNameFromRelationship(string schemaName, string entityName,  ref bool isArrayRequired, EntityMetadata eData)
        {
            string key = ""; 
            // Find the relationship that is referenced.
            OneToManyRelationshipMetadata ERM21 = eData.ManyToOneRelationships.FirstOrDefault(w1 => w1.SchemaName.ToLower().Equals(schemaName.ToLower()));
            ManyToManyRelationshipMetadata ERM2M = eData.ManyToManyRelationships.FirstOrDefault(w2 => w2.SchemaName.ToLower().Equals(schemaName.ToLower()));
            OneToManyRelationshipMetadata ER12M = eData.OneToManyRelationships.FirstOrDefault(w3 => w3.SchemaName.ToLower().Equals(schemaName.ToLower()));

            // Determine which one hit
            if (ERM21 != null)
            {
                isArrayRequired = true;
                key = ERM21.ReferencedEntityNavigationPropertyName;
            }
            else if (ERM2M != null)
            {
                isArrayRequired = true;
                if (ERM2M.Entity1LogicalName.ToLower().Equals(entityName))
                {
                    key = ERM2M.Entity1NavigationPropertyName;
                }
                else
                {
                    key = ERM2M.Entity2NavigationPropertyName;
                }
            }
            else if (ER12M != null)
            {
                key = ER12M.ReferencingAttribute;
            }

            return key; 
        }

        /// <summary>
        /// Parses Key attribute collection for alt key support.
        /// </summary>
        /// <param name="keyValues">alt key's for object</param>
        /// <returns>webAPI compliant key string</returns>
        internal static string ParseAltKeyCollection(KeyAttributeCollection keyValues)
        {
            string keycollection = string.Empty;
            foreach (var itm in keyValues)
            {
                if (itm.Value is EntityReference er)
                {
                    keycollection += $"_{itm.Key}_value={er.Id.ToString("P")},";
                }
                else
                {
                    if (itm.Value is int iValue)
                    {
                        keycollection += $"{itm.Key}={iValue.ToString(CultureInfo.InvariantCulture)},";
                    }
                    else if (itm.Value is float fValue)
                    {
                        keycollection += $"{itm.Key}={fValue.ToString(CultureInfo.InvariantCulture)},";
                    } 
                    else if (itm.Value is OptionSetValue oValue)
                    {
                        keycollection += $"{itm.Key}={oValue.Value},";
                    }
                    else if (itm.Value is DateTime dtValue) // Note : Should work for 'datetime types' may not work for date only types.
                    {
                        keycollection += $"{itm.Key}={dtValue.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)},";
                    }
                    else
                        keycollection += $"{itm.Key}='{itm.Value.ToString().Replace("'", "''")}',";
                }
            }
            return keycollection.Remove(keycollection.Length - 1); // remove trailing ,
        }
        /// <summary>
        /// List of entities to retry retrieves on.
        /// </summary>
        private static List<string> _autoRetryRetrieveEntityList = null;

        /// <summary>
        /// if the Incoming query has an entity on the retry list, returns true.  else returns false.
        /// </summary>
        /// <param name="queryStringToParse">string containing entity name to check against</param>
        /// <returns>true if found, false if not</returns>
        internal static bool ShouldAutoRetryRetrieveByEntityName(string queryStringToParse)
        {
            if (_autoRetryRetrieveEntityList == null)
            {
                _autoRetryRetrieveEntityList = new List<string>
                {
                    "asyncoperation", // to support failures when looking for async Jobs.
                    "importjob" // to support failures when looking for importjob.
                };
            }

            foreach (var itm in _autoRetryRetrieveEntityList)
            {
                if (queryStringToParse.Contains(itm)) return true;
            }
            return false;
        }

        /// <summary>
        /// Creates or Adds scopes and returns the current scope
        /// </summary>
        /// <param name="scopeToAdd"></param>
        /// <param name="currentScopes"></param>
        /// <returns></returns>
        internal static List<string> AddScope(string scopeToAdd, List<string> currentScopes = null)
        {
            if (currentScopes == null)
                currentScopes = new List<string>();

            if (!currentScopes.Contains(scopeToAdd))
            {
                currentScopes.Add(scopeToAdd);
            }

            return currentScopes;
        }


        /// <summary>
        /// Request Headers used by comms to Dataverse
        /// </summary>
        internal static class RequestHeaders
        {
            /// <summary>
            /// Populated with the host process
            /// </summary>
            public const string USER_AGENT_HTTP_HEADER = "User-Agent";
            /// <summary>
            /// Session ID used to track all operations associated with a given group of calls.
            /// </summary>
            public const string X_MS_CLIENT_SESSION_ID = "x-ms-client-session-id";
            /// <summary>
            /// PerRequest ID used to track a specific request.
            /// </summary>
            public const string X_MS_CLIENT_REQUEST_ID = "x-ms-client-request-id";
            /// <summary>
            /// Content type of WebAPI request.
            /// </summary>
            public const string CONTENT_TYPE = "Content-Type";
            /// <summary>
            /// Header loaded with the AADObjectID of the user to impersonate
            /// </summary>
            public const string AAD_CALLER_OBJECT_ID_HTTP_HEADER = "CallerObjectId";
            /// <summary>
            /// Header loaded with the Dataverse user ID of the user to impersonate
            /// </summary>
            public const string CALLER_OBJECT_ID_HTTP_HEADER = "MSCRMCallerID";
            /// <summary>
            /// Header used to pass the token for the user
            /// </summary>
            public const string AUTHORIZATION_HEADER = "Authorization";
            /// <summary>
            /// Header requesting the connection be kept alive.
            /// </summary>
            public const string CONNECTION_KEEP_ALIVE = "Keep-Alive";
            /// <summary>
            /// Header requiring Cache Consistency Server side.
            /// </summary>
            public const string FORCE_CONSISTENCY = "Consistency";

            /// <summary>
            /// This key used to indicate if the custom plugins need to be bypassed during the execution of the request.
            /// </summary>
            public const string BYPASSCUSTOMPLUGINEXECUTION = "BypassCustomPluginExecution";

            /// <summary>
            /// key used to apply the operation to a given solution.
            /// See: https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
            /// </summary>
            public const string SOLUTIONUNIQUENAME = "SolutionUniqueName";

            /// <summary>
            /// used to apply duplicate detection behavior to a given request.
            /// See: https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/use-messages#passing-optional-parameters-with-a-request
            /// </summary>
            public const string SUPPRESSDUPLICATEDETECTION = "SuppressDuplicateDetection";

            /// <summary>
            /// used to pass data though Dataverse to a plugin or downstream system on a request.
            /// See: https://docs.microsoft.com/en-us/powerapps/developer/common-data-service/org-service/use-messages#add-a-shared-variable-from-the-organization-service
            /// </summary>
            public const string TAG = "tag";

            /// <summary>
            /// used to identify concurrencybehavior property in an organization request.
            /// </summary>
            public const string CONCURRENCYBEHAVIOR = "ConcurrencyBehavior";

            /// <summary>
            /// Dataverse Platform Property Prefix
            /// </summary>
            public const string DATAVERSEHEADERPROPERTYPREFIX = "MSCRM.";

        }

        internal static class ResponseHeaders
        {
            /// <summary>
            /// Recomended number of client connection threads Hint 
            /// </summary>
            public const string RECOMMENDEDDEGREESOFPARALLELISM = "x-ms-dop-hint";

            /// <summary>
            /// header for Cookie's
            /// </summary>
            public const string SETCOOKIE = "Set-Cookie";
        }

        /// <summary>
        /// Minim Version numbers for various features of Dataverse API's.
        /// </summary>
        internal static class FeatureVersionMinimums
        {
            /// <summary>
            /// returns true of the feature version is valid for this environment.
            /// </summary>
            /// <param name="instanceVersion">Instance version of the Dataverse Instance</param>
            /// <param name="featureVersion">MinFeatureVersion</param>
            /// <returns></returns>
            internal static bool IsFeatureValidForEnviroment(Version instanceVersion, Version featureVersion)
            {
                if (instanceVersion != null && (instanceVersion >= featureVersion))
                    return true;
                else
                    return false;
            }

            /// <summary>
            /// Lowest server version that can be connected too.
            /// </summary>
            internal static Version DataverseVersionForThisAPI = new Version("5.0.9688.1533");

            /// <summary>
            /// Minimum version that supports batch Operations.
            /// </summary>
            internal static Version BatchOperations = new Version("5.0.9690.3000");

            /// <summary>
            /// Minimum version that supports holding solutions.
            /// </summary>
            internal static Version ImportHoldingSolution = new Version("7.2.0.9");

            /// <summary>
            /// Minimum version that supports the Internal Upgrade Flag
            /// </summary>
            internal static Version InternalUpgradeSolution = new Version("9.0.0.0");

            /// <summary>
            /// MinVersion that supports AAD Caller ID.
            /// </summary>
            internal static Version AADCallerIDSupported = new Version("8.1.0.0");

            /// <summary>
            /// MinVersion that supports Session ID Telemetry Tracking.
            /// </summary>
            internal static Version SessionTrackingSupported = new Version("9.0.2.0");

            /// <summary>
            /// MinVersion that supports Forcing Cache Sync.
            /// </summary>
            internal static Version ForceConsistencySupported = new Version("9.1.0.0");

            /// <summary>
            /// Minimum version to allow plug in bypass param.
            /// </summary>
            internal static Version AllowBypassCustomPlugin = new Version("9.1.0.20918");

            /// <summary>
            /// Minimum version supported by the Web API
            /// </summary>
            internal static Version WebAPISupported = new Version("8.0.0.0");

            /// <summary>
            /// Minimum version supported for AsyncRibbonProcessing.
            /// </summary>
            internal static Version AllowAsyncRibbonProcessing = new Version("9.1.0.15400");

            /// <summary>
            /// Minimum version supported for Passing Component data to Dataverse as part of solution deployment..
            /// </summary>
            internal static Version AllowComponetInfoProcessing = new Version("9.1.0.16547");

            /// <summary>
            /// Minimum version support for Solution tagging.
            /// </summary>
            internal static Version AllowTemplateSolutionImport = new Version("9.2.21013.00131");

            /// <summary>
            /// Minimum version support for ImportSolutionAsync API.
            /// </summary>
            internal static Version AllowImportSolutionAsyncV2 = new Version("9.2.21013.00131");


        }

        #region CookieHelpers
        /// <summary>
        /// Manage Pushing Cookies Forward in a switchable manner.
        /// </summary>
        /// <param name="strHeader">Header string to start with</param>
        /// <param name="cookieCollection">Collection of cookies currently in the system</param>
        /// <returns></returns>
        internal static ConcurrentDictionary<string, string> GetAllCookiesFromHeader(string strHeader, ConcurrentDictionary<string, string> cookieCollection)
        {
            ArrayList al = ConvertCookieHeaderToArrayList(strHeader);
            return ConvertCookieArraysToCookieDictionary(al, cookieCollection);
        }

        /// <summary>
        /// Manage Pushing Cookies Forward in a switchable manner.
        /// </summary>
        /// <param name="strHeaderList">Header string to start with</param>
        /// <param name="cookieCollection"> collection of cookies currently in the system</param>
        /// <returns></returns>
        internal static ConcurrentDictionary<string, string> GetAllCookiesFromHeader(string[] strHeaderList, ConcurrentDictionary<string, string> cookieCollection)
        {
            if (strHeaderList != null)
            {
                return ConvertCookieArraysToCookieDictionary(ConvertCookieListToArrayList(strHeaderList), cookieCollection);
            }
            else
            {
                return cookieCollection;
            }
        }

        internal static string GetCookiesFromCollectionAsString(ConcurrentDictionary<string, string> cookieCollection)
        {
            if (cookieCollection == null || cookieCollection.Count == 0)
                return string.Empty;

            string cookieString = "";
            if (cookieCollection != null)
            {
                foreach (var itm in cookieCollection)
                {
                    cookieString += $"{itm.Key}={itm.Value};";
                }
            }
            return cookieString;
        }

        internal static List<string> GetCookiesFromCollectionAsArray(ConcurrentDictionary<string, string> cookieCollection)
        {
            if (cookieCollection == null || cookieCollection.Count == 0)
                return null;

            string s = "";
            List<string> cookieItems = new List<string>();
            foreach (var itm in cookieCollection)
            {
                //cookieItems.Add($"{itm.Key}={itm.Value}; ");
                s += $"{itm.Key}={itm.Value}; ";
            }
            cookieItems.Add(s);
            return cookieItems;
        }

        /// <summary>
        /// Create an array list of cookies
        /// </summary>
        /// <param name="strCookHeader"></param>
        /// <returns></returns>
        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            if (string.IsNullOrEmpty(strCookHeader))
                return null;

            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');

            return ConvertCookieListToArrayList(strCookTemp);

        }

        private static ArrayList ConvertCookieListToArrayList(string[] potentalCookieList)
        {
            ArrayList al = new ArrayList();
            int i = 0;
            int n = potentalCookieList.Length;
            try
            {
                while (i < n)
                {
                    if (potentalCookieList[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        if (n == (i + 1))
                        {
                            al.Add(potentalCookieList[i]);
                        }
                        else
                        {
                            al.Add(potentalCookieList[i] + "," + potentalCookieList[i + 1]);
                        }
                        i++;
                    }
                    else
                    {
                        al.Add(potentalCookieList[i]);
                    }
                    i++;
                }
            }
            finally { } // No op.. let it fail if it could not parse the cookie.
            return al;
        }

        /// <summary>
        /// Generate Cookie collection for the Array.
        /// </summary>
        /// <param name="al"></param>
        /// <param name="cookieCollection"> Cookie collection to populate or update</param>
        /// <returns></returns>
        private static ConcurrentDictionary<string, string> ConvertCookieArraysToCookieDictionary(ArrayList al, ConcurrentDictionary<string, string> cookieCollection)
        {
            if (cookieCollection == null)
                cookieCollection = new ConcurrentDictionary<string, string>();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                Cookie cookTemp = new Cookie();

                string strCNameAndCValue = strEachCookParts[0];
                if (!string.IsNullOrEmpty(strCNameAndCValue))
                {
                    int firstEqual = strCNameAndCValue.IndexOf("=");
                    string firstName = strCNameAndCValue.Substring(0, firstEqual);
                    string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));
                    cookTemp.Name = firstName;
                    cookTemp.Value = allValue;
                    if (cookieCollection.ContainsKey(firstName))
                        cookieCollection[firstName] = allValue;
                    else
                        cookieCollection.TryAdd(firstName, allValue);
                }
            }
            return cookieCollection;
        }
        #endregion

        #region HTTPHeaderCleanupSupport
        /// <summary>
        /// Fix for issue in .net core which is not using proper separators for User-Agent and Server Headers
        /// </summary>
        /// <param name="headerCollection">Collection to clean up values for</param>
        /// <returns></returns>
        internal static void CleanUpHeaderKeys(WebHeaderCollection headerCollection)
        {
            if (headerCollection.AllKeys.Contains(RequestHeaders.USER_AGENT_HTTP_HEADER))
            {
                string UserAgentValue = headerCollection[RequestHeaders.USER_AGENT_HTTP_HEADER];
                if (UserAgentValue.Contains(","))
                {
                    headerCollection[RequestHeaders.USER_AGENT_HTTP_HEADER] = UserAgentValue.Replace(",", " ");
                }
            }
        }
        #endregion

    }
}
