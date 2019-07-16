using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
