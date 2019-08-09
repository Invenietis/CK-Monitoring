using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Monitoring
{
    /// <summary>
    /// Writes <see cref="IMulticastLogEntry"/> to a <see cref="StringBuilder"/>.
    /// This object is not thread-safe. If it must be used in a concurrent manner, 
    /// a lock should protect it.
    /// </summary>
    public class MulticastLogEntryTextBuilder
    {
        const int _maxMonitorCount = 64 * 64 * 64;
        static readonly char[] _b64e = new char[] {  '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                       'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                       'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                       'U', 'V', 'W', 'X', 'Y', 'Z',
                       'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
                       'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
                       'u', 'v', 'w', 'x', 'y', 'z',
                       '+', '/'};
        readonly StringBuilder _builder;
        readonly Dictionary<Guid, string> _monitorNames;
        DateTime _lastLogTime;
        readonly bool _useDeltaTime;
        readonly string _timeFormat;
        readonly int _timeFormatLength;
        readonly string _blankSpacePrefix;
        const string _deltaFormat = @"ss\.fffffff";
        readonly int _deltaBlankSpacing;

        /// <summary>
        /// Initializes a new instance of <see cref="MulticastLogEntryTextBuilder"/>.
        /// </summary>
        /// <param name="useDeltaTime">
        /// When set to false, the log times are displayed with the +delta seconds from its minute: the full time appears
        /// only once per minute.
        /// </param>
        /// <param name="timeInMilliseconds">True to cut the fractional part to milliseconds, otherwise nanoseconds are displayed.</param>
        public MulticastLogEntryTextBuilder( bool useDeltaTime, bool timeInMilliseconds )
            : this( timeInMilliseconds ? @"yyyy-MM-dd HH\hmm.ss.fff" : FileUtil.FileNameUniqueTimeUtcFormat, useDeltaTime )
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MulticastLogEntryTextBuilder"/>.
        /// </summary>
        /// <param name="timeFormat">Time format string used to display the DateTime before each logged line.</param>
        /// <param name="useDeltaTime">
        /// When set to false, the log times are displayed with the +delta seconds from its minute: the full time appears
        /// only once per minute.
        /// </param>
        public MulticastLogEntryTextBuilder( string timeFormat, bool useDeltaTime )
        {
            _useDeltaTime = useDeltaTime;
            _builder = new StringBuilder();
            _monitorNames = new Dictionary<Guid, string>();
            _timeFormat = timeFormat;
            _timeFormatLength = DateTime.UtcNow.ToString( timeFormat ).Length;
            _blankSpacePrefix = new string( ' ', _timeFormatLength + 8 ); //timeString + ' ' + '~001' + ' ' + 'F' + ' '
            _deltaBlankSpacing = _timeFormatLength - _deltaFormat.Length;
        }

        static string B64ConvertInt( int value )
        {
            //https://www.codeproject.com/Articles/27493/Convert-an-integer-to-a-base-64-string-and-back-ag
            // length should be 3 only
            char[] c = new char[3];
            c[0] = _b64e[(value & 258048) >> 12];
            c[1] = _b64e[(value & 4032) >> 06];
            c[2] = _b64e[(value & 63)];
            return new string( c );
        }

        string GetFormattedDate( IMulticastLogEntry e )
        {
            string output;
            // Log time prefixes the first line only.
            TimeSpan delta = e.LogTime.TimeUtc - _lastLogTime;
            if( !_useDeltaTime || delta >= TimeSpan.FromMinutes( 1 ) )
            {
                output = e.LogTime.TimeUtc.ToString( _timeFormat );
                _lastLogTime = e.LogTime.TimeUtc;
            }
            else
            {
                output = _deltaBlankSpacing + delta.ToString( _deltaFormat );
            }
            return output;
        }

        /// <summary>
        /// Represent a "line" to display.
        /// This is used to change the color of the different parts: 
        /// </summary>
        public readonly struct FormattedEntry
        {
            /// <summary>
            /// Construct a new <see cref="FormattedEntry"/>.
            /// </summary>
            /// <param name="logLevel">Log level.</param>
            /// <param name="indentationPrefix">Indentation prefix.</param>
            /// <param name="monitorId">The monitor id, the constructor will prepend a '~' character.</param>
            /// <param name="date">The formatted date of the entry.</param>
            /// <param name="entryText">The rest of the text entry.</param>
            public FormattedEntry( char logLevel, string indentationPrefix, string monitorId, string date, string entryText )
            {
                LogLevel = logLevel;
                IndentationPrefix = indentationPrefix;
                FormattedDate = date;
                MonitorId = "~" + monitorId;
                EntryText = entryText;
            }

            /// <summary>
            /// The level character.
            /// </summary>
            public readonly char LogLevel;

            /// <summary>
            /// The Indentation of the log.
            /// </summary>
            public readonly string IndentationPrefix;
                
            /// <summary>
            /// Part 1: the formatted date of the entry.
            /// </summary>
            public readonly string FormattedDate;

            /// <summary>
            /// Part 2: the monitor identifier to display.
            /// </summary>
            public readonly string MonitorId;

            /// <summary>
            /// Part 3: the text entry is the <see cref="LogLevel"/> + ' ' + <see cref="IndentationPrefix"/> + the log text itself.
            /// </summary>
            public readonly string EntryText;

            /// <summary>
            /// Returns the log entry formatted: 
            /// <see cref="FormattedDate"/> + " " + <see cref="MonitorId"/> + " " + <see cref="EntryText"/>.
            /// </summary>
            /// <returns>The full formatted entry line.</returns>
            public override string ToString() => FormattedDate + " " + MonitorId + " " + LogLevel + " " + IndentationPrefix + EntryText;

        }

        /// <summary>
        /// Gets a formatted string from a logEntry.
        /// </summary>
        /// <param name="logEntry">Entry to format.</param>
        /// <param name="entrySeparator">Separate the two entries when needed. Default null resolve to <see cref="Environment.NewLine"/>.</param>
        /// <returns>Formatted log entries.</returns>
        public string FormatEntryString(IMulticastLogEntry logEntry, string entrySeparator = null)
        {
            if( entrySeparator == null ) entrySeparator = Environment.NewLine;
            var logOutput = FormatEntry( logEntry );
            if( logOutput.Key == null )
            {
                return logOutput.Value.ToString();
            }
            _builder.Append( logOutput.Key.Value.ToString() )
                .Append( entrySeparator )
                .Append( logOutput.Value.ToString() );
            string output = _builder.ToString();
            _builder.Clear();
            return output;
        }

        /// <summary>
        /// Format the <paramref name="logEntry"/>
        /// </summary>
        /// <param name="logEntry"></param>
        /// <returns>(FormattedEntry optionalEntry, FormattedEntry entry)</returns>
        public KeyValuePair<FormattedEntry?, FormattedEntry> FormatEntry( IMulticastLogEntry logEntry )
        {
            FormattedEntry? firstLine;
            string formattedDate = GetFormattedDate( logEntry );

            char logLevel = CharLogLevel( logEntry );
            string indentationPrefix = ActivityMonitorTextHelperClient.GetMultilinePrefixWithDepth( logEntry.Text != null ? logEntry.GroupDepth : logEntry.GroupDepth - 1 );

            if( !_monitorNames.TryGetValue( logEntry.MonitorId, out string monitorId ) )
            {
                string _monitorResetLog = "";
                if( _monitorNames.Count == _maxMonitorCount )
                {
                    _monitorNames.Clear();
                    _monitorResetLog = $" Monitor reset count {_maxMonitorCount}.";
                }
                monitorId = B64ConvertInt( _monitorNames.Count );
                _monitorNames.Add( logEntry.MonitorId, monitorId );
                firstLine = new FormattedEntry( 'i', indentationPrefix, monitorId, formattedDate, $"Monitor: ~{logEntry.MonitorId.ToString()}. {_monitorResetLog}" );
            }
            else
            {
                firstLine = null;
            }

            string multiLinePrefix = _blankSpacePrefix + indentationPrefix;

            if( logEntry.Text != null )
            {
                Debug.Assert( logEntry.LogType != LogEntryType.CloseGroup );
                if( logEntry.LogType == LogEntryType.OpenGroup ) _builder.Append( "> " );
                multiLinePrefix += "  ";
                _builder.AppendMultiLine( multiLinePrefix, logEntry.Text, false );
                if( logEntry.Exception != null )
                {
                    _builder.AppendLine();
                    logEntry.Exception.ToStringBuilder( _builder, multiLinePrefix, false );
                }
            }
            else
            {
                Debug.Assert( logEntry.Conclusions != null );
                _builder.Append( "< " );
                if( logEntry.Conclusions.Count > 0 )
                {
                    _builder.Append( " | " ).Append( logEntry.Conclusions.Count ).Append( " conclusion" );
                    if( logEntry.Conclusions.Count > 1 ) _builder.Append( 's' );
                    _builder.Append( ':' ).AppendLine();
                    multiLinePrefix += "   | ";
                    foreach( var c in logEntry.Conclusions )
                    {
                        _builder.AppendMultiLine( multiLinePrefix, c.Text, true );
                    }
                }
            }
            string outputLine = _builder.ToString();
            _builder.Clear();
            return new KeyValuePair<FormattedEntry?, FormattedEntry>( firstLine, new FormattedEntry( logLevel, indentationPrefix, monitorId, formattedDate, outputLine ) );
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e">The IMulticastLogEntry being formatted.</param>
        /// <returns>The char corresponding to the LogLevel</returns>
        char CharLogLevel( IMulticastLogEntry e )
        {
            // Level is one char.
            switch( e.LogLevel & LogLevel.Mask )
            {
                case LogLevel.Debug: return 'd';
                case LogLevel.Trace: return ' ';
                case LogLevel.Info: return 'i';
                case LogLevel.Warn: return 'W';
                case LogLevel.Error: return 'E';
                default: return 'F';
            }
        }

        /// <summary>
        /// Resets internal states (like monitor's numbering).
        /// </summary>
        public void Reset()
        {
            _monitorNames.Clear();
            _lastLogTime = DateTime.MinValue;
        }
    }
}
