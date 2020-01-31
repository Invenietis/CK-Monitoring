using System;
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
        ConsoleConfiguration _config;
        string _currentMonitor;
        bool _monitorColorSwitch;
        /// <summary>
        /// Initializes a new console handler.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public Console( ConsoleConfiguration config )
        {
            _config = config ?? throw new ArgumentNullException( "config" );
            if( string.IsNullOrWhiteSpace( config.DateFormat ) )
            {
                _builder = new MulticastLogEntryTextBuilder( config.UseDeltaTime, true );
            }
            else
            {
                _builder = new MulticastLogEntryTextBuilder( config.DateFormat, config.UseDeltaTime );
            }
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
            if( !(c is ConsoleConfiguration cf) ) return false;
            _config = cf;
            return true;
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

            var entry = _builder.FormatEntry( e.Entry );
            if( entry.Key != null )
            {
                DisplayFormattedEntry( entry.Key.Value, LogLevel.Info);
            }
            DisplayFormattedEntry( entry.Value, e.Entry.LogLevel );
        }

        void DisplayFormattedEntry( MulticastLogEntryTextBuilder.FormattedEntry entry, LogLevel logLevel )
        {
            ConsoleColor prevForegroundColor = System.Console.ForegroundColor;
            ConsoleColor prevBackgroundColor = System.Console.BackgroundColor;
            System.Console.Write( entry.FormattedDate + " " );
            if( _currentMonitor != entry.MonitorId )
            {
                _currentMonitor = entry.MonitorId;
                _monitorColorSwitch = !_monitorColorSwitch;
            }
            if( _monitorColorSwitch )
            {
                System.Console.ForegroundColor = prevBackgroundColor;
                System.Console.BackgroundColor = prevForegroundColor;
            }
            System.Console.Write( entry.MonitorId );
            ConsoleSetColor();
            System.Console.Write( " " + entry.LogLevel + " " );
            ConsoleResetColor();
            System.Console.Write( entry.IndentationPrefix );
            ConsoleSetColor();
            System.Console.WriteLine( entry.EntryText );
            ConsoleResetColor();

            void ConsoleSetColor() =>
                ColoredActivityMonitorConsoleClient.DefaultSetColor( _config.BackgroundColor, LogLevel.Mask & logLevel );
            void ConsoleResetColor()
            {
                System.Console.ForegroundColor = prevForegroundColor;
                System.Console.BackgroundColor = prevBackgroundColor;
            }
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
