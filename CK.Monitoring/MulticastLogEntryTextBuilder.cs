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
        readonly StringBuilder _prefixBuilder;
        readonly StringBuilder _builder;
        readonly Dictionary<Guid, string> _monitorNames;
        Guid _currentMonitorId;
        string _currentMonitorName;
        DateTime _lastLogTime;
        int _nameLen;

        /// <summary>
        /// Initializes a new instance of <see cref="MulticastLogEntryTextBuilder"/>.
        /// </summary>
        /// <param name="b">The initial string builder to use.</param>
        public MulticastLogEntryTextBuilder( StringBuilder b = null )
        {
            _prefixBuilder = new StringBuilder();
            _builder = new StringBuilder();
            _monitorNames = new Dictionary<Guid, string>();
        }

        /// <summary>
        /// Gets the <see cref="StringBuilder"/>.
        /// </summary>
        public StringBuilder Builder => _builder;

        /// <summary>
        /// Appends a formatted entry to the <see cref="Builder"/>.
        /// </summary>
        /// <param name="e">The </param>
        public void AppendEntry( IMulticastLogEntry e )
        {
            Debug.Assert( DateTimeStamp.MaxValue.ToString().Length == 32,
                "DateTimeStamp FileNameUniqueTimeUtcFormat and the uniquifier: max => 32 characters long." );
            Debug.Assert( Guid.NewGuid().ToString().Length == 36,
                "Guid => 18 characters long." );

            _prefixBuilder.Append( ' ', _nameLen + 32 );
            _prefixBuilder.Append( "| ", e.Text != null ? e.GroupDepth : e.GroupDepth - 1 );
            string prefix = _prefixBuilder.ToString();
            _prefixBuilder.Clear();
            // MonitorId (if needed) on one line.
            if( _currentMonitorId == e.MonitorId )
            {
                _builder.Append( ' ', _nameLen + 1 );
            }
            else
            {
                _currentMonitorId = e.MonitorId;
                if( !_monitorNames.TryGetValue( _currentMonitorId, out _currentMonitorName ) )
                {
                    _currentMonitorName = _monitorNames.Count.ToString( "X" + _nameLen );
                    int len = _currentMonitorName.Length;
                    if( _nameLen < len )
                    {
                        prefix = " " + prefix;
                        _nameLen = len;
                    }
                    _monitorNames.Add( _currentMonitorId, _currentMonitorName );
                    _builder.Append( _currentMonitorName )
                            .Append( "~~~~" )
                            .Append( ' ', 28 )
                            .Append( "~~ Monitor: " )
                            .AppendLine( _currentMonitorId.ToString() );
                    _builder.Append( ' ', _nameLen + 1 );
                }
                else
                {
                    _builder.Append( _currentMonitorName ).Append( '~' );
                    _builder.Append( ' ', _nameLen - _currentMonitorName.Length );
                }
            }
            // Log time prefixes the first line only.
            TimeSpan delta = e.LogTime.TimeUtc - _lastLogTime;
            if( delta >= TimeSpan.FromMinutes( 1 ) )
            {
                string logTime = e.LogTime.TimeUtc.ToString( FileUtil.FileNameUniqueTimeUtcFormat );
                _builder.Append( ' ' );
                _builder.Append( logTime );
                _builder.Append( ' ' );
                _lastLogTime = e.LogTime.TimeUtc;
            }
            else
            {
                _builder.Append( ' ', 17 );
                _builder.Append( '+' );
                _builder.Append( delta.ToString( @"ss\.fffffff" ) );
                _builder.Append( ' ' );
            }

            // Level is one char.
            char level;
            switch( e.LogLevel & LogLevel.Mask )
            {
                case LogLevel.Debug: level = 'd'; break;
                case LogLevel.Trace: level = ' '; break;
                case LogLevel.Info: level = 'i'; break;
                case LogLevel.Warn: level = 'W'; break;
                case LogLevel.Error: level = 'E'; break;
                default: level = 'F'; break;
            }
            _builder.Append( level );
            _builder.Append( ' ' );
            _builder.Append( "| ", e.Text != null ? e.GroupDepth : e.GroupDepth - 1 );

            if( e.Text != null )
            {
                if( e.LogType == LogEntryType.OpenGroup ) _builder.Append( "> " );
                prefix += "  ";
                _builder.AppendMultiLine( prefix, e.Text, false ).AppendLine();
                if( e.Exception != null )
                {
                    e.Exception.ToStringBuilder( _builder, prefix );
                }
            }
            else
            {
                Debug.Assert( e.Conclusions != null );
                _builder.Append( "< " );
                if( e.Conclusions.Count > 0 )
                {
                    _builder.Append( " | " ).Append( e.Conclusions.Count ).Append( " conclusion" );
                    if( e.Conclusions.Count > 1 ) _builder.Append( 's' );
                    _builder.Append( ':' ).AppendLine();
                    prefix += "   | ";
                    foreach( var c in e.Conclusions )
                    {
                        _builder.AppendMultiLine( prefix, c.Text, true ).AppendLine();
                    }
                }
                else
                {
                    _builder.AppendLine();
                }
            }
        }

        /// <summary>
        /// Clears the <see cref="Builder"/> and resets internal states (like monitor's numbering).
        /// </summary>
        public void Reset()
        {
            _builder.Clear();
            _currentMonitorId = Guid.Empty;
            _monitorNames.Clear();
            _nameLen = 0;
            _lastLogTime = DateTime.MinValue;
        }

        /// <summary>
        /// Overridden to return the <see cref="Builder"/>'s ToString().
        /// </summary>
        /// <returns>The builder's current text.</returns>
        public override string ToString() => _builder.ToString();

    }
}
