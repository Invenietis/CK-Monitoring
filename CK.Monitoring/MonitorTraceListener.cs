using CK.Core;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CK.Monitoring
{
    /// <summary>
    /// An ActivityMonitor trace listener that sends System.Diagnostic traces to the provided GrandOutput
    /// using <see cref="GrandOutput.ExternalLog(LogLevel,string,Exception,CKTrait)"/>.
    /// All log entries sent by it have the tag "TraceListener".
    /// </summary>
    public class MonitorTraceListener : TraceListener
    {
        static readonly CKTrait _tag = ActivityMonitor.Tags.Register( nameof( TraceListener ) );

        /// <summary>
        /// Creates a MonitorTraceListener instance.
        /// </summary>
        /// <param name="grandOutput">The <see cref="Monitoring.GrandOutput"/> to send traces to.</param>
        /// <param name="failFast">
        /// When true <see cref="Environment.FailFast(string)"/> will terminate the application on <see cref="Debug.Assert(bool)"/>, <see cref="Trace.Assert(bool)"/>,
        /// <see cref="Debug.Fail(string)"/> and <see cref="Trace.Fail(string)"/>.
        /// </param>
        public MonitorTraceListener( GrandOutput grandOutput, bool failFast )
        {
            GrandOutput = grandOutput ?? throw new ArgumentNullException( nameof( grandOutput ) );
            FailFast = failFast;
        }

        /// <summary>
        /// Gets whether a call to <see cref="Fail(string)"/> calls <see cref="Environment.FailFast(string)"/>.
        /// This can be changed at runtime at any time.
        /// </summary>
        public bool FailFast { get; set; }

        /// <summary>
        /// Gets the associated grand output.
        /// </summary>
        public GrandOutput GrandOutput { get; }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, string, CKTrait)"/> with a <see cref="LogLevel.Trace"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void Write( string message )
        {
            GrandOutput.ExternalLog( LogLevel.Trace, message, _tag );
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, string, CKTrait)"/> with a <see cref="LogLevel.Trace"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void WriteLine( string message )
        {
            GrandOutput.ExternalLog( LogLevel.Trace, message, _tag );
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, string, CKTrait)"/> with a <see cref="LogLevel.Fatal"/>
        /// and, if <see cref="FailFast"/> is true, calls <see cref="Environment.FailFast(string)"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void Fail( string message )
        {
            GrandOutput.ExternalLog( LogLevel.Fatal, message, _tag );
            if( FailFast )
            {
                GrandOutput.Dispose();
                Environment.FailFast( message );
            }
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, string, CKTrait)"/> with a <see cref="LogLevel.Fatal"/>
        /// and, if <see cref="FailFast"/> is true, calls <see cref="Environment.FailFast(string)"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void Fail( string message, string detailMessage )
        {
            string msg = message;
            if( !string.IsNullOrEmpty( detailMessage ) )
            {
                msg += ": " + detailMessage;
            }
            GrandOutput.ExternalLog( LogLevel.Fatal, msg, _tag );
            if( FailFast )
            {
                GrandOutput.Dispose();
                Environment.FailFast( msg );
            }
        }

        /// <summary>
        /// Calls <see cref="GrandOutput.ExternalLog"/> with the <paramref name="id"/>, <paramref name="message"/>. The log level is based on <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventCache">A System.Diagnostics.TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the System.Diagnostics.TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="message">A message to write.</param>
        public override void TraceEvent( TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message )
        {
            GrandOutput.ExternalLog( GetLogLevel( eventType ), BuildMessage( message, null, source, id ), _tag );
        }

        /// <summary>
        /// Calls <see cref="GrandOutput.ExternalLog"/> with the <paramref name="id"/>, <paramref name="message"/>. The log level is based on <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventCache">A System.Diagnostics.TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the System.Diagnostics.TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="format">A message with placeholders to write.</param>
        /// <param name="args">The message arguments.</param>
        public override void TraceEvent( TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args )
        {
            GrandOutput.ExternalLog( GetLogLevel( eventType ), BuildMessage( format, args, source, id ), _tag );
        }

        static LogLevel GetLogLevel( TraceEventType eventType )
        {
            switch( eventType )
            {
                case TraceEventType.Verbose:
                    return LogLevel.Trace;
                case TraceEventType.Information:
                    return LogLevel.Info;
                case TraceEventType.Warning:
                    return LogLevel.Warn;
                case TraceEventType.Error:
                    return LogLevel.Error;
                case TraceEventType.Critical:
                    return LogLevel.Fatal;
                case TraceEventType.Start:
                case TraceEventType.Stop:
                case TraceEventType.Resume:
                case TraceEventType.Suspend:
                case TraceEventType.Transfer:
                    return LogLevel.Info;
                default:
                    return LogLevel.Trace;
            }
        }

        static string BuildMessage( string format, object[] args = null, string source = null, int? id = null )
        {
            StringBuilder sb = new StringBuilder();
            if( !string.IsNullOrEmpty( source ) ) sb.Append( $"[{source}] " );
            if( id.HasValue && id.Value != 0 ) sb.Append( $"<{id.Value}> " );
            if( args != null && args.Length > 0 )
            {
                sb.AppendFormat( CultureInfo.InvariantCulture, format, args );
            }
            else
            {
                sb.Append( format );
            }
            return sb.ToString();
        }
    }
}
