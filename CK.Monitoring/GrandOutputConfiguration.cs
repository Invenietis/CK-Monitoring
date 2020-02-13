using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Monitoring
{
    /// <summary>
    /// Configure a <see cref="GrandOutput"/>.
    /// </summary>
    public class GrandOutputConfiguration
    {
        internal bool InternalClone;

        /// <summary>
        /// Gets or sets the timer duration.
        /// Defaults to 500 milliseconds.
        /// </summary>
        public TimeSpan TimerDuration { get; set; } = TimeSpan.FromMilliseconds( 500 );

        /// <summary>
        /// Gets or sets the minimal filter of all the <see cref="IActivityMonitor"/> that are bound
        /// to the <see cref="GrandOutput"/> (through the <see cref="GrandOutputClient"/>).
        /// Default to <see cref="LogFilter.Undefined"/>: there is no impact on each <see cref="IActivityMonitor.ActualFilter"/>.
        /// </summary>
        public LogFilter MinimalFilter { get; set; } = LogFilter.Undefined;

        /// <summary>
        /// Gets or sets the filter level for <see cref="GrandOutput.ExternalLog(LogLevel, string, Exception, CKTrait)"/> methods.
        /// Defaults to <see cref="LogLevelFilter.None"/> (the <see cref="ActivityMonitor.DefaultFilter"/>.<see cref="LogFilter.Line">Line</see>
        /// is used).
        /// </summary>
        public LogLevelFilter ExternalLogLevelFilter { get; set; } = LogLevelFilter.None;

        /// <summary>
        /// Gets the list of handlers configuration.
        /// </summary>
        public List<IHandlerConfiguration> Handlers { get; } = new List<IHandlerConfiguration>();

        /// <summary>
        /// Sets the <see cref="TimerDuration"/> (fluent interface).
        /// </summary>
        /// <param name="duration">Sets the timer duration.</param>
        /// <returns>This configuration.</returns>
        public GrandOutputConfiguration SetTimerDuration( TimeSpan duration )
        {
            TimerDuration = duration;
            return this;
        }

        /// <summary>
        /// Adds a handler configuration (fluent interface).
        /// </summary>
        /// <param name="config">The configuration top add.</param>
        /// <returns>This configuration object.</returns>
        public GrandOutputConfiguration AddHandler( IHandlerConfiguration config )
        {
            Handlers.Add( config );
            return this;
        }

        /// <summary>
        /// Clones this configuration.
        /// </summary>
        /// <returns>Clone of this configuration.</returns>
        public GrandOutputConfiguration Clone()
        {
            var c = new GrandOutputConfiguration();
            c.TimerDuration = TimerDuration;
            c.Handlers.AddRange( Handlers.Select( h => h.Clone() ) );
            return c;
        }
    }
}
