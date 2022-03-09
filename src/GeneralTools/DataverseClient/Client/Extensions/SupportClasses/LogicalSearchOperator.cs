
namespace Microsoft.PowerPlatform.Dataverse.Client.Extensions
{
    /// <summary>
    /// Logical Search Pram to apply to over all search.
    /// </summary>
    public enum LogicalSearchOperator
    {
        /// <summary>
        /// Do not apply the Search Operator
        /// </summary>
        None = 0,
        /// <summary>
        /// Or Search
        /// </summary>
        Or = 1,
        /// <summary>
        /// And Search
        /// </summary>
        And = 2
    }
}
