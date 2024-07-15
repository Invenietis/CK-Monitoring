using CK.Core;
using CK.Monitoring.Hosting;
using CK.Monitoring;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Adds extension methods on <see cref="IHostApplicationBuilder"/>.
    /// </summary>
    public static class HostApplicationBuilderMonitoringExtensions
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
        public static IActivityMonitor GetBuilderMonitor( this IHostApplicationBuilder builder )
        {
            var monitor = (IActivityMonitor?)builder.Properties.GetValueOrDefault( typeof( IActivityMonitor ) );
            if( monitor == null )
            {
                bool hasInitializer = builder.Properties.ContainsKey( typeof( GrandOutputConfigurator ) );
                monitor = hasInitializer
                            ? new ActivityMonitor( nameof( IHostApplicationBuilder ) )
                            : new ActivityMonitor( ActivityMonitorOptions.WithInitialReplay | ActivityMonitorOptions.SkipAutoConfiguration, nameof( IHostApplicationBuilder ) );
                builder.Properties[typeof( IActivityMonitor )] = monitor;
            }
            return monitor;
        }

        /// <summary>
        /// Initializes the <see cref="GrandOutput.Default"/> from the "CK-Monitoring" <see cref="IHostApplicationBuilder.Configuration"/> section.
        /// This automatically registers a <see cref="IActivityMonitor"/> as a scoped service in the services.
        /// <para>
        /// This can safely be called multiple times on the same <paramref name="builder"/> but this should not be used
        /// on a host created inside an already running application (this would reconfigure the GrandOutput.Default).
        /// </para>
        /// <para>
        /// Note that no registrations is done for IActivityMonitor. The standard registration is:
        /// <code>
        ///   // The ActivityMonitor is not mapped, only the IActivityMonitor must 
        ///   // be exposed and the ParallelLogger is the one of the monitor.
        ///   services.AddScoped&lt;IActivityMonitor, ActivityMonitor&gt;();
        ///   services.AddScoped(sp => sp.GetRequiredService&lt;IActivityMonitor&gt;().ParallelLogger );
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="builder">Host builder</param>
        /// <returns>The builder.</returns>
        public static T UseCKMonitoring<T>( this T builder ) where T : IHostApplicationBuilder
        {
            var t = typeof( GrandOutputConfigurator );
            if( !builder.Properties.ContainsKey( t ) )
            {
                builder.Properties.Add( t, t );
                new GrandOutputConfigurator( builder );
            }
            return builder;
        }

    }
}
