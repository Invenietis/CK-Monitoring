using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Monitoring
{
    /// <summary>
    /// Configures a <see cref="GrandOutput"/>.
    /// </summary>
    public class GrandOutputConfiguration
    {
        internal bool InternalClone;

        /// <summary>
        /// Gets or sets the timer duration. This is used to enable housekeeping features such
        /// as cleaning up the weak references to the <see cref="GrandOutputClient"/> and calling
        /// the <see cref="IGrandOutputHandler.OnTimer(IActivityMonitor, TimeSpan)"/>.
        /// Defaults to 500 milliseconds.
        /// </summary>
        public TimeSpan? TimerDuration { get; set; } = null;

        /// <summary>
        /// Gets or sets a <see cref="LogFilter"/> that will eventually affect all the <see cref="IActivityMonitor.ActualFilter"/> of
        /// monitors that are bound to the <see cref="GrandOutput"/> (through the <see cref="GrandOutputClient"/>).
        /// This, when not null, impacts the <see cref="GrandOutput.MinimalFilter"/> property that defaults
        /// to <see cref="LogFilter.Undefined"/>: by default, there is no impact on each <see cref="IActivityMonitor.ActualFilter"/>.
        /// </summary>
        public LogFilter? MinimalFilter { get; set; } = null;

        /// <summary>
        /// Gets or sets the filter level for <see cref="GrandOutput.ExternalLog(LogLevel, string, Exception, CKTrait)"/> methods.
        /// This, when not null, impacts the <see cref="GrandOutput.ExternalLogLevelFilter"/> property that defaults to <see cref="LogLevelFilter.None"/>
        /// (the <see cref="ActivityMonitor.DefaultFilter"/>.<see cref="LogFilter.Line">Line</see> is used).
        /// </summary>
        public LogLevelFilter? ExternalLogLevelFilter { get; set; } = null;

        /// <summary>
        /// Gets or sets whether unhandled exceptions from <see cref="AppDomain.UnhandledException"/>
        /// and <see cref="System.Threading.Tasks.TaskScheduler.UnobservedTaskException"/> are sent to
        /// the <see cref="GrandOutput.ExternalLog(LogLevel, string, CKTrait?)"/>.
        /// <para>
        /// Defaults to true for the <see cref="GrandOutput.Default"/> instance and false for the other ones.
        /// </para>
        /// </summary>
        public bool? TrackUnhandledExceptions { get; set; } = null;

        /// <summary>
        /// Gets the list of handlers configuration.
        /// </summary>
        public List<IHandlerConfiguration> Handlers { get; } = new List<IHandlerConfiguration>();

        /// <summary>
        /// Sets the <see cref="MinimalFilter"/> (fluent interface).
        /// </summary>
        /// <param name="filter">The log filter.</param>
        /// <returns>This configuration.</returns>
        public GrandOutputConfiguration SetMinimalFilter( LogFilter? filter )
        {
            MinimalFilter = filter;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ExternalLogLevelFilter"/> (fluent interface).
        /// </summary>
        /// <param name="filter">The log level filter.</param>
        /// <returns>This configuration.</returns>
        public GrandOutputConfiguration SetExternalLogLevelFilter( LogLevelFilter? filter )
        {
            ExternalLogLevelFilter = filter;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="TrackUnhandledExceptions"/> (fluent interface).
        /// </summary>
        /// <param name="track">The value.</param>
        /// <returns>This configuration.</returns>
        public GrandOutputConfiguration SetTrackUnhandledExceptions( bool? track )
        {
            TrackUnhandledExceptions = track;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="TimerDuration"/> (fluent interface).
        /// </summary>
        /// <param name="duration">The root timer duration.</param>
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
            var c = new GrandOutputConfiguration
            {
                TimerDuration = TimerDuration,
                ExternalLogLevelFilter = ExternalLogLevelFilter,
                MinimalFilter = MinimalFilter,
                TrackUnhandledExceptions = TrackUnhandledExceptions
            };
            c.Handlers.AddRange( Handlers.Select( h => h.Clone() ) );
            return c;
        }
    }
}
