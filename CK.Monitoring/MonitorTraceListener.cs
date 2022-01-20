using CK.Core;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CK.Monitoring
{
    /// <summary>
    /// A trace listener that sends System.Diagnostic traces to the provided GrandOutput.
    /// All log entries sent by it have the tag "TraceListener".
    /// When <see cref="FailFast"/> is true, a <see cref="MonitoringFailFastException"/> is thrown instead of
    /// calling <see cref="Environment.FailFast(string)"/>.
    /// <para>
    /// The <see cref="GrandOutput.Default"/> creates an instance of this listener and, by default,
    /// removes all the other ones.
    /// See the detailed comment of <see cref="GrandOutput.EnsureActiveDefault(GrandOutputConfiguration, bool)"/>.
    /// </para>
    /// <para>
    /// If the behavior regarding <see cref="Trace.Listeners"/> must be changed, please exploit this <see cref="TraceListenerCollection"/> that is wide open
    /// to any modifications and the fact that <see cref="MonitorTraceListener"/> exposes its associated grand output and
    /// that <see cref="MonitorTraceListener.FailFast"/> can be changed at any time.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// After a lot of thoughts and experiments we concluded that fail fast on <see cref="Debug.Assert(bool)"/> and other <see cref="Trace.Fail(string)"/>
    /// should be an opt-in choice: at the early stage of a project, we often deploy applications compiled in Debug and such deployments should behave
    /// as most as possible as Release ones.
    /// <para>
    /// We generally don't use the "fail fast" approach in our architecture. One of the main reason is the using IDisposable (kind of) RAII pattern that
    /// is heavily used in .Net, fail fast breaks this pattern .
    /// </para>
    /// </para>
    /// </remarks>
    public class MonitorTraceListener : TraceListener
    {
        /// <summary>
        /// Tag used for all the logs sent.
        /// </summary>
        public static readonly CKTrait TraceListener = ActivityMonitor.Tags.Register( nameof( TraceListener ) );

        /// <summary>
        /// Creates a MonitorTraceListener instance.
        /// </summary>
        /// <param name="grandOutput">The <see cref="Monitoring.GrandOutput"/> to send traces to.</param>
        /// <param name="failFast">
        /// When true <see cref="Environment.FailFast(string)"/> will terminate the application on <see cref="Debug.Assert(bool)"/>, <see cref="Trace.Assert(bool)"/>,
        /// <see cref="Debug.Fail(string)"/> and <see cref="Trace.Fail(string)"/>.
        /// See this <see cref="MonitorTraceListener"/>'s remarks section to understand why this should be false.
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
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with a <see cref="LogLevel.Trace"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void Write( string? message )
        {
            if( message != null ) GrandOutput.ExternalLog( LogLevel.Trace, TraceListener, message: message );
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with a <see cref="LogLevel.Trace"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void WriteLine( string? message )
        {
            if( message != null ) GrandOutput.ExternalLog( LogLevel.Trace, TraceListener, message );
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with a <see cref="LogLevel.Fatal"/>
        /// and, if <see cref="FailFast"/> is true, calls <see cref="Environment.FailFast(string)"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        public override void Fail( string? message )
        {
            message ??= "Fail called with no message.";
            GrandOutput.ExternalLog( LogLevel.Fatal, TraceListener, message );
            if( FailFast )
            {
                GrandOutput.Dispose();
                Environment.FailFast( message );
            }
            else
            {
                throw new MonitoringFailFastException( message );
            }
        }

        /// <summary>
        /// Overridden to call <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with a <see cref="LogLevel.Fatal"/>
        /// and, if <see cref="FailFast"/> is true, calls <see cref="Environment.FailFast(string)"/>.
        /// </summary>
        /// <param name="message">The text to log.</param>
        /// <param name="detailMessage">A detail message that will be appended to the text.</param>
        public override void Fail( string? message, string? detailMessage )
        {
            string msg = message ?? "Fail called with no message.";
            if( !string.IsNullOrEmpty( detailMessage ) )
            {
                msg += " " + detailMessage;
            }
            Fail( msg );
        }

        /// <summary>
        /// Calls <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with the <paramref name="id"/>
        /// and formatted message.
        /// The log level is based on <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventCache">A System.Diagnostics.TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the System.Diagnostics.TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="message">A message to write.</param>
        public override void TraceEvent( TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message )
        {
            GrandOutput.ExternalLog( GetLogLevel( eventType ), TraceListener, BuildMessage( message, null, source, id ) );
        }

        /// <summary>
        /// Calls <see cref="GrandOutput.ExternalLog(LogLevel, CKTrait, string, Exception?)"/> with the <paramref name="id"/>
        /// and formatted message. The log level is based on <paramref name="eventType"/>.
        /// </summary>
        /// <param name="eventCache">A System.Diagnostics.TraceEventCache object that contains the current process ID, thread ID, and stack trace information.</param>
        /// <param name="source">A name used to identify the output, typically the name of the application that generated the trace event.</param>
        /// <param name="eventType">One of the System.Diagnostics.TraceEventType values specifying the type of event that has caused the trace.</param>
        /// <param name="id">A numeric identifier for the event.</param>
        /// <param name="format">A message with placeholders to write.</param>
        /// <param name="args">The message arguments.</param>
        public override void TraceEvent( TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args )
        {
            GrandOutput.ExternalLog( GetLogLevel( eventType ), TraceListener, BuildMessage( format, args, source, id ) );
        }

        static LogLevel GetLogLevel( TraceEventType eventType )
        {
            return eventType switch
            {
                TraceEventType.Verbose => LogLevel.Trace,
                TraceEventType.Information => LogLevel.Info,
                TraceEventType.Warning => LogLevel.Warn,
                TraceEventType.Error => LogLevel.Error,
                TraceEventType.Critical => LogLevel.Fatal,
                TraceEventType.Start or TraceEventType.Stop or TraceEventType.Resume or TraceEventType.Suspend or TraceEventType.Transfer => LogLevel.Info,
                _ => LogLevel.Trace,
            };
        }

        static string BuildMessage( string? format, object?[]? args = null, string? source = null, int? id = null )
        {
            StringBuilder sb = new StringBuilder();
            if( !string.IsNullOrEmpty( source ) ) sb.Append( $"[{source}] " );
            if( id.HasValue && id.Value != 0 ) sb.Append( $"<{id.Value}> " );
            if( args != null && args.Length > 0 )
            {
                sb.AppendFormat( CultureInfo.InvariantCulture, format ?? "No Trace format provided.", args );
            }
            else
            {
                sb.Append( format );
            }
            return sb.ToString();
        }
    }
}
