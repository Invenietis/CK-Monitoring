using CK.Monitoring.Hosting;
using CK.Core;
using CK.Monitoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;

namespace Microsoft.Extensions.Hosting
{

    /// <summary>
    /// Adds extension methods on <see cref="IHostBuilder"/>.
    /// </summary>
    public static class HostBuilderMonitoringHostExtensions
    {
        /// <summary>
        /// Gets an activity monitor for this builder.
        /// <para>
        /// This can always be called: logs in this monitor are retained and emitted once the code injected by <see cref="UseCKMonitoring(IHostBuilder)"/>
        /// is executed during <see cref="IHostBuilder.Build()"/>.  
        /// </para>
        /// </summary>
        /// <param name="builder">This builder.</param>
        /// <returns>A monitor for the host and application builder.</returns>
        public static IActivityMonitor GetBuilderMonitor( this IHostBuilder builder ) => GetBuilderMonitor( builder.Properties );

        static IActivityMonitor GetBuilderMonitor( IDictionary<object, object> props )
        {
            var monitor = (IActivityMonitor?)props.GetValueOrDefault( typeof( IActivityMonitor ) );
            if( monitor == null )
            {
                monitor = new ActivityMonitor( false, nameof( IHostBuilder ) );
                monitor.Output.RegisterClient( new BuilderMonitorReplayClient() );
                props[typeof( IActivityMonitor )] = monitor;
            }
            return monitor;
        }

        /// <summary>
        /// Gets an activity monitor for this builder context.
        /// <para>
        /// This can always be called: logs in this monitor are retained and emitted once the code injected by <see cref="UseCKMonitoring(IHostBuilder)"/>
        /// is executed during <see cref="IHostBuilder.Build()"/>.  
        /// </para>
        /// </summary>
        /// <param name="builder">This builder context.</param>
        /// <returns>A monitor for the host and application builder.</returns>
        public static IActivityMonitor GetBuilderMonitor( this HostBuilderContext ctx ) => GetBuilderMonitor( ctx.Properties );

        /// <summary>
        /// Initializes the <see cref="GrandOutput.Default"/> and bounds the configuration to the configuration section "CK-Monitoring".
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// </summary>
        /// <param name="builder">Host builder</param>
        /// <returns>The builder.</returns>
        public static IHostBuilder UseCKMonitoring( this IHostBuilder builder )
        {
            return DoUseMonitoring( builder, null, c => c.GetSection( "CK-Monitoring" ) );
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
        public static IHostBuilder UseMonitoring( this IHostBuilder builder, GrandOutput grandOutput, string configurationPath )
        {
            if( grandOutput == null ) throw new ArgumentNullException( nameof( grandOutput ) );
            if( grandOutput == GrandOutput.Default ) throw new ArgumentException( "The GrandOutput must not be the default one.", nameof( grandOutput ) );
            return DoUseMonitoring( builder, grandOutput, c => c.GetSection( configurationPath ) );
        }

        /// <summary>
        /// The configuration for the default <see cref="GrandOutput.Default"/> section must now always be "CK-Monitoring".
        /// Application settings and other configuration sources MUST be updated accordingly.
        /// <see cref="UseCKMonitoring(IHostBuilder)"/> must now be called.
        /// </summary>
        /// <param name="builder">This host builder</param>
        /// <param name="section">The configuration section. Must not be null.</param>
        /// <returns>The builder.</returns>
        [Obsolete( "Call UseCKMonitoring and updates the configuration section to be \"CK-Monitoring\".", true )]
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
        /// <param name="builder">This Web host builder.</param>
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

        static IHostBuilder DoUseMonitoring( IHostBuilder builder, GrandOutput? grandOutput, Func<IConfiguration, IConfigurationSection> configSection )
        {
            var initializer = new GrandOutputConfigurationInitializer( grandOutput );
            initializer.ConfigureBuilder( builder, configSection );
            return builder;
        }

    }
}
