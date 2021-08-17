#region using
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client.Utils
{
    /// <summary>
    /// Organization request/response extenstions
    /// </summary>
    public static class RequestResponseExtenstions
    {
        /// <summary>
        /// Converts OrganizationRequest object to ExpandoObject
        /// </summary>
        /// <param name="request">Organization request to convert</param>
        /// <returns></returns>
        internal static ExpandoObject ToExpandoObject(this OrganizationRequest request)
        {
            var result = new ExpandoObject() as IDictionary<string, Object>;
            object propertyValue;
            var requestType = request.GetType();

            // If request is OrganizationRequest get properties from Parameters collection otherwise use public properties
            if (requestType == typeof(OrganizationRequest))
            {
                foreach (var parameter in request.Parameters)
                {
                    result.Add(parameter.Key, parameter.Value);
                }
            }
            else
            {
                foreach (var property in request.GetType().GetProperties())
                {
                    if (property.DeclaringType == typeof(OrganizationRequest))
                        continue;

                    propertyValue = property.GetValue(request);
                    result.Add(property.Name, propertyValue);
                }
            }

            return (ExpandoObject)result;
        }

    }
}
