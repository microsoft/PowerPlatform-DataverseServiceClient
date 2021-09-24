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
        #region Dataverse Interaction Settings
        private int _maxRetryCount = Utils.AppSettingsHelper.GetAppSetting("ApiOperationRetryCountOverride", 10);

        /// <summary>
        /// Number of retries for an execute operation
        /// </summary>
        public int MaxRetryCount
        {
            get => _maxRetryCount;
            set => _maxRetryCount = value;
        }


        private TimeSpan _retryPauseTime = Utils.AppSettingsHelper.GetAppSettingTimeSpan("ApiOperationRetryDelayOverride", Utils.AppSettingsHelper.TimeSpanFromKey.Seconds, new TimeSpan(0, 0, 0, 5));

        /// <summary>
        /// Amount of time to wait between retries
        /// </summary>
        public TimeSpan RetryPauseTime
        {
            get => _retryPauseTime;
            set => _retryPauseTime = value;
        }

        private bool _useWebApi = Utils.AppSettingsHelper.GetAppSetting<bool>("UseWebApi", true);

        /// <summary>
        /// Use Web API instead of org service
        /// </summary>
        public bool UseWebApi
        {
            get => _useWebApi;
            set => _useWebApi = value;
        }

        private bool _useWebApiLoginFlow = Utils.AppSettingsHelper.GetAppSetting<bool>("UseWebApiLoginFlow", false);
        /// <summary>
        /// Use Web API instead of org service for logging into and getting boot up data.
        /// </summary>
        public bool UseWebApiLoginFlow
        {
            get => _useWebApiLoginFlow;
            set => _useWebApiLoginFlow = value;
        }

        #endregion

        #region MSAL Settings.
        private TimeSpan _msalTimeout = Utils.AppSettingsHelper.GetAppSettingTimeSpan("MSALRequestTimeoutOverride", Utils.AppSettingsHelper.TimeSpanFromKey.Seconds, new TimeSpan(0, 0, 0, 30));

        /// <summary>
        /// Amount of time to wait for MSAL/AAD to wait for a token response before timing out
        /// </summary>
        public TimeSpan MSALRequestTimeout
        {
            get => _msalTimeout;
            set => _msalTimeout = value;
        }

        private int _msalRetryCount = Utils.AppSettingsHelper.GetAppSetting("MSALRequestRetryCountOverride", 3);

        /// <summary>
        /// Number of retries to Get a token from MSAL.
        /// </summary>
        public int MsalRetryCount
        {
            get => _msalRetryCount;
            set => _msalRetryCount = value;
        }

        private bool _msalEnablePIIInLog = Utils.AppSettingsHelper.GetAppSetting("MSALLogPII", false);

        /// <summary>
        /// Enabled Logging of PII in MSAL Log. - defaults to false.
        /// </summary>
        public bool MSALEnabledLogPII
        {
            get => _msalEnablePIIInLog;
            set => _msalEnablePIIInLog = value;
        }
        #endregion
    }
}
