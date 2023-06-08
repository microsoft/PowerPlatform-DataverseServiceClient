using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/powerapps/developer/data-platform/org-service/web-service-error-codes
    /// </summary>
    internal static class ErrorCodes
    {
        /// <summary>
        /// Combined execution time of incoming requests exceeded limit of {0} milliseconds over time window of {1} seconds. Decrease number of concurrent requests or reduce the duration of requests and try again later.
        /// </summary>
        public const int ThrottlingTimeExceededError = unchecked((int)0x80072321); // -2147015903

        /// <summary>
        /// Number of requests exceeded the limit of {0} over time window of {1} seconds.
        /// </summary>
        public const int ThrottlingBurstRequestLimitExceededError = unchecked((int)0x80072322); // -2147015902

        /// <summary>
        /// Number of concurrent requests exceeded the limit of {0}.
        /// </summary>
        public const int ThrottlingConcurrencyLimitExceededError = unchecked((int)0x80072326); // -2147015898

        /// <summary>
        /// Dataverse ServiceClient is not Initialized 
        /// </summary>
        public const int DataverseServiceClientNotIntialized = unchecked((int)0x8004426C); // -2147204500

        /// <summary>
        /// Solution Path and or File Stream is Null
        /// </summary>
        public const int SolutionFilePathNull = unchecked((int)0x800443FC); // - 2147204100;

        /// <summary>
        /// Operation is not valid onprem.
        /// </summary>
        public const int OperationInvalidOnPrem = unchecked((int)0x80044262); // -2147204510; 

    }
}
