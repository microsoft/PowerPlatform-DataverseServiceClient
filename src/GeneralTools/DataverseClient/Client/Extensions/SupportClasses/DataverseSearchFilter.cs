using System;
using Microsoft.Xrm.Sdk.Query;
using System.Collections.Generic;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Dataverse Filter class.
    /// </summary>
    public class DataverseSearchFilter
    {
        /// <summary>
        /// List of Dataverse Filter conditions
        /// </summary>
        public List<DataverseFilterConditionItem> SearchConditions { get; set; }
        /// <summary>
        /// Dataverse Filter Operator
        /// </summary>
        public LogicalOperator FilterOperator { get; set; }

        /// <summary>
        /// Creates an empty Dataverse Search Filter.
        /// </summary>
        public DataverseSearchFilter()
        {
            SearchConditions = new List<DataverseFilterConditionItem>();
        }
    }
}
