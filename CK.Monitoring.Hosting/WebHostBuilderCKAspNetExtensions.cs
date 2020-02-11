using CK.Monitoring.Hosting;
using CK.Core;
using CK.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;

namespace Microsoft.AspNetCore.Hosting
{

    /// <summary>
    /// Adds extension methods on <see cref="IHostBuilder"/>.
    /// </summary>
    public static class WebHostBuilderCKAspNetExtensions
    {
        /// <summary>
        /// Uses <see cref="CK.Monitoring"/> during the web host building and initializes the default <see cref="GrandOutput"/>,
        /// and bounds the configuration from the given configuration section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">Web host builder</param>
        /// <param name="configurationPath">The path of the monitoring configuration in the global configuration.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, string configurationPath = "Monitoring" )
        {
            return DoUseMonitoring( builder, null, configurationPath );
        }

        /// <summary>
        /// Uses <see cref="CK.Monitoring"/> during the web host building and initializes an instance of the <see cref="GrandOutput"/>
        /// that must not be null nor be the <see cref="GrandOutput.Default"/> and bounds the configuration from the given configuration section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">Web host builder</param>
        /// <param name="grandOutput">The target <see cref="GrandOutput"/>.</param>
        /// <param name="configurationPath">The path of the monitoring configuration in the global configuration.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, GrandOutput grandOutput, string configurationPath = "Monitoring" )
        {
            if( grandOutput == null ) throw new ArgumentNullException( nameof( grandOutput ) );
            if( grandOutput == GrandOutput.Default ) throw new ArgumentException( "The GrandOutput must not be the default one.", nameof( grandOutput ) );
            return DoUseMonitoring( builder, grandOutput, configurationPath );
        }

        /// <summary>
        /// Uses <see cref="CK.Monitoring"/> during the web host building and initializes the default <see cref="GrandOutput"/>,
        /// and bounds the configuration to the given configuration section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">Web host builder</param>
        /// <param name="section">The configuration section.</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, IConfigurationSection section )
        {
            if( section == null ) throw new ArgumentNullException( nameof( section ) );
            return DoUseMonitoring( builder, null, section );
        }

        /// <summary>
        /// Uses <see cref="CK.Monitoring"/> during the web host building and initializes an instance of the <see cref="GrandOutput"/>
        /// that must not be null nor be the <see cref="GrandOutput.Default"/> and bounds the configuration to a configuration section.
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
            return DoUseMonitoring( builder, grandOutput, section );
        }

        static IHostBuilder DoUseMonitoring( IHostBuilder builder, GrandOutput grandOutput, string configurationPath )
        {
            // Three steps initialization:
            // First creates the initializer instance.
            var initializer = new GrandOutputConfigurationInitializer( grandOutput );

            initializer.InitializeMonitor( builder, configurationPath );

            // Now, registers the PostInstanciationFilter as a transient object.
            // This startup filter will inject the Application service IApplicationLifetime.
            return RegisterMonitor( builder );
        }



        /// <summary>
        /// Initialize from IConfigurationSection instead of configurationPath.
        /// </summary>
        static IHostBuilder DoUseMonitoring( IHostBuilder builder, GrandOutput grandOutput, IConfigurationSection configuration )
        {
            var initializer = new GrandOutputConfigurationInitializer( grandOutput );
            initializer.InitializeMonitor( builder, configuration );
            return RegisterMonitor( builder );
        }

        static IHostBuilder RegisterMonitor( IHostBuilder builder )
        {
            return builder.ConfigureServices( ( ctx, services ) =>
            {
                services.TryAddScoped<IActivityMonitor>( sp => new ActivityMonitor() );
            } );
        }

    }
}
