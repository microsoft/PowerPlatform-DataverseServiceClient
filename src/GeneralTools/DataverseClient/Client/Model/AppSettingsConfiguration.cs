#region
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#endregion

namespace Microsoft.PowerPlatform.Dataverse.Client.Model
{
    /// <summary>
    /// App settings configuration
    /// </summary>
    public class AppSettingsConfiguration
    {
        private int _maxRetryCount = Utils.AppSettingsHelper.GetAppSetting("ApiOperationRetryCountOverride", 10);

        /// <summary>
        /// Number of retries for an execute operation
        /// </summary>
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set => _maxRetryCount = value;
        }

        private TimeSpan _retryPauseTime = Utils.AppSettingsHelper.GetAppSetting("ApiOperationRetryDelayOverride", new TimeSpan(0, 0, 0, 5));

        /// <summary>
        /// Amount of time to wait between retries
        /// </summary>
        public TimeSpan RetryPauseTime
        {
            get => _retryPauseTime;
            set => _retryPauseTime = value;
        }

        private bool _useWebApi = Utils.AppSettingsHelper.GetAppSetting<bool>("UseWebApi", false);

        /// <summary>
        /// Use Web API instead of legacy SOAP org service
        /// </summary>
        public bool UseWebApi
        {
            get => _useWebApi;
            set => _useWebApi = value;
        }
    }
}
