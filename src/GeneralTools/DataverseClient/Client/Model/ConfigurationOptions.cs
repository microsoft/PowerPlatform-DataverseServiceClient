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
    /// Client Configuration Options Array.
    /// </summary>
    public class ConfigurationOptions
    {
        /// <summary>
        /// Updates the instance of Options with a previously created Options Object. 
        /// </summary>
        /// <param name="options">PreLoaded Options Array</param>
        public void UpdateOptions(ConfigurationOptions options)
        {
            if (options != null)
            {
                EnableAffinityCookie = options.EnableAffinityCookie;
                MaxBufferPoolSizeOverride = options.MaxBufferPoolSizeOverride;
                MaxFaultSizeOverride = options.MaxFaultSizeOverride;
                MaxReceivedMessageSizeOverride = options.MaxReceivedMessageSizeOverride;
                MaxRetryCount = options.MaxRetryCount;
                MSALEnabledLogPII = options.MSALEnabledLogPII;
                MSALRequestTimeout = options.MSALRequestTimeout;
                MSALRetryCount = options.MSALRetryCount;
                RetryPauseTime = options.RetryPauseTime;
                UseWebApi = options.UseWebApi;
                UseWebApiLoginFlow = options.UseWebApiLoginFlow;
                UseExponentialRetryDelayForConcurrencyThrottle = options.UseExponentialRetryDelayForConcurrencyThrottle;
            }
        }

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

        private bool _useWebApi = Utils.AppSettingsHelper.GetAppSetting<bool>("UseWebApi", false);

        /// <summary>
        /// Use Web API instead of org service
        /// </summary>
        public bool UseWebApi
        {
            get => _useWebApi;
            set => _useWebApi = value;
        }

        private bool _useExponentialRetryDelayForConcurrencyThrottle = Utils.AppSettingsHelper.GetAppSetting<bool>("UseExponentialRetryDelayForConcurrencyThrottle", false);

        /// <summary>
        /// Use exponential retry delay for concurrency throttling instead of server specified Retry-After header
        /// </summary>
        public bool UseExponentialRetryDelayForConcurrencyThrottle
        {
            get => _useExponentialRetryDelayForConcurrencyThrottle;
            set => _useExponentialRetryDelayForConcurrencyThrottle = value;
        }

        private bool _useWebApiLoginFlow = Utils.AppSettingsHelper.GetAppSetting<bool>("UseWebApiLoginFlow", true);
        /// <summary>
        /// Use Web API instead of org service for logging into and getting boot up data.
        /// </summary>
        public bool UseWebApiLoginFlow
        {
            get => _useWebApiLoginFlow;
            set => _useWebApiLoginFlow = value;
        }

        private bool _enableAffinityCookie = Utils.AppSettingsHelper.GetAppSetting<bool>("EnableAffinityCookie", true);
        /// <summary>
        /// Defaults to True.
        /// <para>When true, this setting applies the default connection routing strategy to connections to Dataverse.</para>
        /// <para>This will 'prefer' a given node when interacting with Dataverse which improves overall connection performance.</para>
        /// <para>When set to false, each call to Dataverse will be routed to any given node supporting your organization. </para>
        /// <para>See https://docs.microsoft.com/en-us/powerapps/developer/data-platform/api-limits#remove-the-affinity-cookie for proper use.</para>
        /// </summary>
        public bool EnableAffinityCookie
        {
            get => _enableAffinityCookie;
            set => _enableAffinityCookie = value;
        }

        // For future work...

        //private TimeSpan _maxConnectionTimeout = Utils.AppSettingsHelper.GetAppSettingTimeSpan("MaxDataverseConnectionTimeOutMinutes", Utils.AppSettingsHelper.TimeSpanFromKey.Minutes, TimeSpan.FromMinutes(4));

        ///// <summary>
        ///// Max connection timeout property
        ///// https://docs.microsoft.com/en-us/azure/app-service/faq-availability-performance-application-issues#why-does-my-request-time-out-after-230-seconds
        ///// Azure Load Balancer has a default idle timeout setting of four minutes. This is generally a reasonable response time limit for a web request.
        ///// </summary>
        //public TimeSpan MaxConnectionTimeout
        //{
        //    get => _maxConnectionTimeout;
        //    set => _maxConnectionTimeout = value;

        //}

        private string _maxFaultSizeOverride = Utils.AppSettingsHelper.GetAppSetting<string>("MaxFaultSizeOverride", null);
        /// <summary>
        /// MaxFaultSize override. - Use under Microsoft Direction only. 
        /// </summary>
        public string MaxFaultSizeOverride
        {
            get => _maxFaultSizeOverride;
            set => _maxFaultSizeOverride = value;
        }

        private string _maxReceivedMessageSize = Utils.AppSettingsHelper.GetAppSetting<string>("MaxReceivedMessageSizeOverride", null);
        /// <summary>
        /// MaxReceivedMessageSize override. - Use under Microsoft Direction only. 
        /// </summary>
        public string MaxReceivedMessageSizeOverride
        {
            get => _maxReceivedMessageSize;
            set => _maxReceivedMessageSize = value;
        }

        private string _maxBufferPoolSizeOveride = Utils.AppSettingsHelper.GetAppSetting<string>("MaxBufferPoolSizeOverride", null);
        /// <summary>
        /// MaxBufferPoolSize override. - Use under Microsoft Direction only. 
        /// </summary>
        public string MaxBufferPoolSizeOverride
        {
            get => _maxBufferPoolSizeOveride;
            set => _maxBufferPoolSizeOveride = value;
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
        public int MSALRetryCount
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
