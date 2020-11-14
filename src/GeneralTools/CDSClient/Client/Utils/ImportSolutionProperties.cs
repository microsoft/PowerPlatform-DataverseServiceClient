using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Cds.Client
{
    /// <summary>
    /// Properties valid for the extraParameters collection of ImportSolution.
    /// </summary>
    public static class ImportSolutionProperties
    {
        /// <summary>
        /// Parameter used to change the default layering behavior during solution import
        /// </summary>
        public static string DESIREDLAYERORDERPARAM = "DesiredLayerOrder";
        /// <summary>
        /// Parameter used to specify whether Solution Import processed ribbon metadata asynchronously
        /// </summary>
        public static string ASYNCRIBBONPROCESSING = "AsyncRibbonProcessing";
        /// <summary>
        /// Parameter used to pass the solution name - Telemetry only
        /// </summary>
        public static string SOLUTIONNAMEPARAM = "SolutionName";
        /// <summary>
        /// Parameter used to pass a collection of component parameters to the import job.
        /// </summary>
        public static string COMPONENTPARAMETERSPARAM = "ComponentParameters";
    }
}
