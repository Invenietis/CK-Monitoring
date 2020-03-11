using CK.Monitoring.Hosting;
using CK.Core;
using CK.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace Microsoft.Extensions.Hosting
{

    /// <summary>
    /// Adds extension methods on <see cref="IHostBuilder"/>.
    /// </summary>
    public static class HostBuilderMonitoringHostExtensions
    {
        /// <summary>
        /// Initializes the <see cref="GrandOutput.Default"/> and bounds the configuration from the given configuration section path.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">Host builder</param>
        /// <param name="configurationPath">The path of the monitoring configuration in the global configuration.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, string configurationPath = "Monitoring" )
        {
            return DoUseMonitoring( builder, null, c => c.GetSection( configurationPath ) );
        }

        /// <summary>
        /// Configures a <see cref="GrandOutput"/> instance that must not be null nor be the <see cref="GrandOutput.Default"/> and
        /// bounds the configuration from the given configuration section path.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">This Host builder</param>
        /// <param name="grandOutput">The target <see cref="GrandOutput"/>.</param>
        /// <param name="configurationPath">The path of the monitoring configuration in the global configuration.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, GrandOutput grandOutput, string configurationPath = "Monitoring" )
        {
            if( grandOutput == null ) throw new ArgumentNullException( nameof( grandOutput ) );
            if( grandOutput == GrandOutput.Default ) throw new ArgumentException( "The GrandOutput must not be the default one.", nameof( grandOutput ) );
            return DoUseMonitoring( builder, grandOutput, c => c.GetSection( configurationPath ) );
        }

        /// <summary>
        /// Configures the <see cref="GrandOutput.Default"/> based on the given configuration section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">This host builder</param>
        /// <param name="section">The configuration section. Must not be null.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, IConfigurationSection section )
        {
            if( section == null ) throw new ArgumentNullException( nameof( section ) );
            return DoUseMonitoring( builder, null, c => section );
        }

        /// <summary>
        /// Configures a <see cref="GrandOutput"/> instance that must not be null nor be the <see cref="GrandOutput.Default"/> and
        /// bounds the configuration from the given configuration section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">This Web host builder</param>
        /// <param name="grandOutput">The target <see cref="GrandOutput"/>.</param>
        /// <param name="section">The configuration section.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, GrandOutput grandOutput, IConfigurationSection section )
        {
            if( grandOutput == null ) throw new ArgumentNullException( nameof( grandOutput ) );
            if( grandOutput == GrandOutput.Default ) throw new ArgumentException( "The GrandOutput must not be the default one.", nameof( grandOutput ) );
            if( section == null ) throw new ArgumentNullException( nameof( section ) );
            return DoUseMonitoring( builder, grandOutput, c => section );
        }

        static IHostBuilder DoUseMonitoring( IHostBuilder builder, GrandOutput grandOutput, Func<IConfiguration, IConfigurationSection> configSection )
        {
            var initializer = new GrandOutputConfigurationInitializer( grandOutput );
            initializer.ConfigureBuilder( builder, configSection );
            return builder;
        }

    }
}
