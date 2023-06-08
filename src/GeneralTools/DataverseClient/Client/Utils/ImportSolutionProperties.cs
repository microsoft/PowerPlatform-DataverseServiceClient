using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerPlatform.Dataverse.Client
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
        /// <summary>
        /// Direct the system to convert any matching unmanaged customizations into your managed solution
        /// </summary>
        public static string CONVERTTOMANAGED = "ConvertToManaged";
        /// <summary>
        /// Internal use only
        /// </summary>
        public static string TEMPLATESUFFIX = "TemplateSuffix";
        /// <summary>
        /// Internal use only
        /// </summary>
        public static string ISTEMPLATEMODE = "IsTemplateMode";
        /// <summary>
        /// When set to true, causes ImportSolution process to use the Stage and Upgrade Process. 
        /// </summary>
        public static string USESTAGEANDUPGRADEMODE = "StageAndUpgradeSolution";

    }
}
