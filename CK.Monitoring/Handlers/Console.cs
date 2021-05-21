using System;
using System.Drawing;
using System.Text;
using CK.Core;
using Pastel;

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
            // Ensure the System.Console colors are actually valid when starting.
            // Linux consoles may start with an invalid and undocumented value (-1).
            // In System.Console's source code, -1 is internally referenced as "UnknownColor".
            if( !Enum.IsDefined( typeof(ConsoleColor), System.Console.BackgroundColor ) )
                System.Console.BackgroundColor = ConsoleColor.Black;

            if( !Enum.IsDefined( typeof(ConsoleColor), System.Console.ForegroundColor ) )
                System.Console.ForegroundColor = ConsoleColor.White;

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
                DisplayFormattedEntry( entry.Key.Value, LogLevel.Info );
            }
            DisplayFormattedEntry( entry.Value, e.Entry.LogLevel );
        }

        static readonly Color[] _colors = new Color[] { //handpicked
                Color.FromArgb( 0x00, 0x00, 0xD5 ), Color.FromArgb( 0xD5, 0xB5, 0x00 ), Color.FromArgb( 0xD5, 0x20, 0x00 ), Color.FromArgb( 0x00, 0xD5, 0x95 ),
                Color.FromArgb( 0xD5, 0x00, 0x9F ), Color.FromArgb( 0x85, 0x00, 0xD5 ), Color.FromArgb( 0xD5, 0x95, 0x00 ), Color.FromArgb( 0x00, 0xD5, 0x1B ),
                Color.FromArgb( 0x00, 0x95, 0xD5 ), Color.FromArgb( 0x9F, 0x00, 0xD5 ), Color.FromArgb( 0xCA, 0xD5, 0x00 ), Color.FromArgb( 0x40, 0x00, 0xD5 ),
                Color.FromArgb( 0xD5, 0x6A, 0x00 ), Color.FromArgb( 0xD5, 0x00, 0x2B ), Color.FromArgb( 0x00, 0xD5, 0xD5 ), Color.FromArgb( 0x6A, 0x00, 0xD5 ),

                Color.FromArgb( 0x50, 0x50, 0x85 ), Color.FromArgb( 0x85, 0x7D, 0x50 ), Color.FromArgb( 0x85, 0x58, 0x50 ), Color.FromArgb( 0x50, 0x85, 0x75 ),
                Color.FromArgb( 0x85, 0x50, 0x78 ), Color.FromArgb( 0x71, 0x50, 0x85 ), Color.FromArgb( 0x85, 0x75, 0x50 ), Color.FromArgb( 0x50, 0x85, 0x56 ),
                Color.FromArgb( 0x50, 0x75, 0x85 ), Color.FromArgb( 0x78, 0x50, 0x85 ), Color.FromArgb( 0x83, 0x85, 0x50 ), Color.FromArgb( 0x60, 0x50, 0x85 ),
                Color.FromArgb( 0x85, 0x6A, 0x50 ), Color.FromArgb( 0x85, 0x50, 0x5A ), Color.FromArgb( 0x50, 0x85, 0x85 ), Color.FromArgb( 0x6A, 0x50, 0x85 ),

                Color.FromArgb( 0x1B, 0x1B, 0xBA ), Color.FromArgb( 0xBA, 0xA3, 0x1B ), Color.FromArgb( 0xBA, 0x33, 0x1B ), Color.FromArgb( 0x1B, 0xBA, 0x8A ),
                Color.FromArgb( 0xBA, 0x1B, 0x93 ), Color.FromArgb( 0x7E, 0x1B, 0xBA ), Color.FromArgb( 0xBA, 0x8A, 0x1B ), Color.FromArgb( 0x1B, 0xBA, 0x2F ),
                Color.FromArgb( 0x1B, 0x8A, 0xBA ), Color.FromArgb( 0x93, 0x1B, 0xBA ), Color.FromArgb( 0xB3, 0xBA, 0x1B ), Color.FromArgb( 0x4A, 0x1B, 0xBA ),
                Color.FromArgb( 0xBA, 0x6A, 0x1B ), Color.FromArgb( 0xBA, 0x1B, 0x3A ), Color.FromArgb( 0x1B, 0xBA, 0xBA ), Color.FromArgb( 0x6A, 0x1B, 0xBA ),

                Color.FromArgb( 0x35, 0x35, 0x9F ), Color.FromArgb( 0x9F, 0x8F, 0x35 ), Color.FromArgb( 0x9F, 0x45, 0x35 ), Color.FromArgb( 0x35, 0x9F, 0x80 ),
                Color.FromArgb( 0x9F, 0x35, 0x85 ), Color.FromArgb( 0x78, 0x35, 0x9F ), Color.FromArgb( 0x9F, 0x80, 0x35 ), Color.FromArgb( 0x35, 0x9F, 0x43 ),
                Color.FromArgb( 0x35, 0x80, 0x9F ), Color.FromArgb( 0x85, 0x35, 0x9F ), Color.FromArgb( 0x9A, 0x9F, 0x35 ), Color.FromArgb( 0x55, 0x35, 0x9F ),
                Color.FromArgb( 0x9F, 0x6A, 0x35 ), Color.FromArgb( 0x9F, 0x35, 0x4A ), Color.FromArgb( 0x35, 0x9F, 0x9F ), Color.FromArgb( 0x6A, 0x35, 0x9F ) };

        static readonly Color[] _foregroundColor = new Color[] { Color.Black, Color.Aquamarine, Color.Gold, Color.White };
        static readonly char[] _b64e = new char[] {  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                       'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                       'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                       'U', 'V', 'W', 'X', 'Y', 'Z',
                       'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                       'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                       'u', 'v', 'w', 'x', 'y', 'z',
                       '+', '/'};
        StringBuilder _sb = new StringBuilder();
        void DisplayFormattedEntry( MulticastLogEntryTextBuilder.FormattedEntry entry, LogLevel logLevel )
        {
            _sb.Clear();
            ConsoleColor prevForegroundColor = System.Console.ForegroundColor;
            ConsoleColor prevBackgroundColor = System.Console.BackgroundColor;
            _sb.Append( entry.FormattedDate )
                .Append( ' ' );
            if( _currentMonitor != entry.MonitorId )
            {
                _currentMonitor = entry.MonitorId;
                _monitorColorSwitch = !_monitorColorSwitch;
            }
            if( _config.EnableMonitorIdColorFlag )
            {
                string monitorId = entry.MonitorId;
                _sb.Append( monitorId[0] );
                for( int i = 1; i < monitorId.Length; i++ )
                {
                    int b64 = _b64e.IndexOf( s => s == monitorId[i] );
                    if( b64 != -1 )
                    {
                        _sb.Append( monitorId[i].ToString().Pastel( _foregroundColor[b64 / 16] ).PastelBg( _colors[b64] ) );
                    }
                    else
                    {
                        _sb.Append( monitorId[i] );
                    }
                }
            }
            else
            {
                if( _monitorColorSwitch )
                {
                    System.Console.ForegroundColor = prevBackgroundColor;
                    System.Console.BackgroundColor = prevForegroundColor;
                }
                _sb.Append( entry.MonitorId.Pastel( System.Console.ForegroundColor ).PastelBg( System.Console.BackgroundColor ) );
            }


            (ConsoleColor background, ConsoleColor foreground) = ColoredActivityMonitorConsoleClient.DefaultColorTheme( prevBackgroundColor, logLevel & LogLevel.Mask );
            _sb.Append( ' ' )
                .Append( entry.LogLevel.ToString().Pastel( foreground ).PastelBg( background ) )
                .Append( ' ' )
                .Append( entry.IndentationPrefix )
                .Append(entry.EntryText.Pastel( foreground ).PastelBg( background ) );
            System.Console.WriteLine( _sb.ToString() );
            _sb.Clear();
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
