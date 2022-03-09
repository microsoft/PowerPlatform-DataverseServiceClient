using Microsoft.Xrm.Sdk.Query;

namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{

    /// <summary>
    /// Dataverse Filter item.
    /// </summary>
    public class DataverseFilterConditionItem
    {
        /// <summary>
        /// Dataverse Field name to Filter on
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// Value to use for the Filter
        /// </summary>
        public object FieldValue { get; set; }
        /// <summary>
        /// Dataverse Operator to apply
        /// </summary>
        public ConditionOperator FieldOperator { get; set; }
    }
}
