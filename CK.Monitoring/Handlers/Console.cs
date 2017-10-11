using System;
using System.Collections.Generic;
using System.Text;
using CK.Core;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Writes logs to the console using the <see cref="MulticastLogEntryTextBuilder"/>
    /// (just like the <see cref="TextFile"/> handler).
    /// </summary>
    public class Console : IGrandOutputHandler
    {
        readonly MulticastLogEntryTextBuilder _builder;

        /// <summary>
        /// Initializes a new console handler.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public Console( ConsoleConfiguration config )
        {
            _builder = new MulticastLogEntryTextBuilder();
        }

        /// <summary>
        /// Initialization of this handler always returns true.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <returns>Always true.</returns>
        public bool Activate( IActivityMonitor m )
        {
            return true;
        }

        /// <summary>
        /// Accepts any configuration that is a <see cref="ConsoleConfiguration"/>.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="c">The configuration.</param>
        /// <returns>True if <paramref name="c"/> is a ConsoleConfiguration.</returns>
        public bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c )
        {
            return c is ConsoleConfiguration;
        }

        /// <summary>
        /// Deactivates this handler.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        public void Deactivate( IActivityMonitor m )
        {
            _builder.Reset();
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="e">The log entry.</param>
        public void Handle( IActivityMonitor m, GrandOutputEventInfo e )
        {
            _builder.AppendEntry( e.Entry );
            System.Console.Write( _builder.Builder.ToString() );
            _builder.Builder.Clear();
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="timerSpan">Indicative timer duration.</param>
        public void OnTimer( IActivityMonitor m, TimeSpan timerSpan )
        {
        }
    }
}
