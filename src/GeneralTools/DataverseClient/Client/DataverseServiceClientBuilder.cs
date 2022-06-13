using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerPlatform.Dataverse.Client.Model;

namespace Microsoft.PowerPlatform.Dataverse.Client
{
    /// <summary>
    /// TBD
    /// </summary>
    public class DataverseServiceClientBuilder
    {
        private ConnectionOptions _connectionOptions;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionOptions"></param>
        internal DataverseServiceClientBuilder(ConnectionOptions connectionOptions)
        {
            _connectionOptions = connectionOptions;
            throw new NotImplementedException("In Development - Not for use.");

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionOptions"></param>
        /// <returns></returns>
        public static DataverseServiceClientBuilder Create(ConnectionOptions connectionOptions)
        {
            return new DataverseServiceClientBuilder(connectionOptions);    
        }

    }

    /// <summary>
    /// TBD
    /// </summary>
    public static class ServiceCollectionExtentions
    {
        /// <summary>
        /// TBD
        /// </summary>
        /// <param name="services"></param>
        /// <param name="connectionOptions"></param>
        /// <returns></returns>
        public static IServiceCollection AddDataverseServiceClient(
            this IServiceCollection services,
            Action<ConnectionOptions> connectionOptions)
        {
            throw new NotImplementedException("In Development - Not for use.");
            
            //services.Configure(connectionOptions);

            
            // Register lib services here...
            // services.AddScoped<ILibraryService, DefaultLibraryService>();

            //return services;
        }
    }


}
