using CK.Core;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace CK.Monitoring.InterProcess
{
    /// <summary>
    /// This receiver is the server of a <see cref="SimplePipeSenderActivityMonitorClient"/>.
    /// It creates a thread that receives the log entries from the external client and inject
    /// them in a local target monitor.
    /// <para>
    /// This receiver and its associated sender client cover a simple scenario where a process
    /// launch another simple process that uses one and only one monitor. The logs emitted by the
    /// child process appear to be from the parent process, incorporated in the activity of the
    /// parent process local monitor.
    /// </para>
    /// </summary>
    public sealed class SimpleLogPipeReceiver : IDisposable
    {
        readonly AnonymousPipeServerStream _server;
        readonly CKBinaryReader _reader;
        readonly IActivityMonitor _monitor;
        readonly Thread _thread;
        readonly bool _interProcess;
        LogReceiverEndStatus _endFlag;

        SimpleLogPipeReceiver( IActivityMonitor m, bool interProcess )
        {
            _interProcess = interProcess;
            var inherit = interProcess ? HandleInheritability.Inheritable : HandleInheritability.None;
            _server = new AnonymousPipeServerStream( PipeDirection.In, inherit );
            _reader = new CKBinaryReader( _server );
            _monitor = m;
            PipeName = _server.GetClientHandleAsString();
            _thread = new Thread( Run )
            {
                IsBackground = true
            };
            _thread.Start();
        }

        /// <summary>
        /// Gets the pipe handler name that must be transmitted to the <see cref="SimplePipeSenderActivityMonitorClient"/>.
        /// </summary>
        public string PipeName { get; }

        /// <summary>
        /// Waits for the termination of the other side.
        /// If it is known that the client has failed (typically because external process ended with a non zero
        /// return code), <paramref name="otherFailed"/> should be true: this receiver will only wait for 500 ms
        /// before returning, avoiding to wait for the internal thread termination.
        /// When <paramref name="otherFailed"/> is false, this method blocks until the client sends its goodbye
        /// message or the pipe is broken.
        /// </summary>
        /// <param name="otherFailed">True when you already know that the sender has failed.</param>
        /// <returns>The final status.</returns>
        public LogReceiverEndStatus WaitEnd( bool otherFailed )
        {
            if( otherFailed )
            {
                if( !_thread.Join( 500 ) ) _endFlag = LogReceiverEndStatus.Error;
            }
            else
            {
                _thread.Join();
            }
            return _endFlag;
        }

        /// <summary>
        /// Waits for the termination of the internal thread and closes the pipe.
        /// Even if this can be called immediately, you should first call <see cref="WaitEnd(bool)"/>
        /// before calling Dispose.
        /// </summary>
        public void Dispose()
        {
            if( _endFlag == LogReceiverEndStatus.None ) _thread.Join();
            _reader.Dispose();
            _server.Dispose();
        }

        void Run()
        {
            try
            {
                int streamVersion = _reader.ReadInt32();
                if( _interProcess ) _server.DisposeLocalCopyOfClientHandle();
                for(; ; )
                {
                    var e = LogEntry.Read( _reader, streamVersion, out bool badEndOfStream );
                    if( e == null || badEndOfStream )
                    {
                        _endFlag = badEndOfStream ? LogReceiverEndStatus.MissingEndMarker : LogReceiverEndStatus.Normal;
                        break;
                    }
                    switch( e.LogType )
                    {
                        case LogEntryType.Line:
                            {
                                if( _monitor.ShouldLogLine( e.LogLevel, e.Tags, out var finalTags ) )
                                {
                                    var d = new ActivityMonitorLogData( e.LogLevel | LogLevel.IsFiltered, finalTags, e.Text, CKException.CreateFrom( e.Exception ), e.FileName, e.LineNumber );
                                    d.SetExplicitLogTime( e.LogTime );
                                    _monitor.UnfilteredLog( ref d );
                                }
                                break;
                            }
                        case LogEntryType.OpenGroup:
                            {
                                ActivityMonitorLogData d;
                                if( _monitor.ShouldLogLine( e.LogLevel, e.Tags, out var finalTags ) )
                                {
                                    d = new ActivityMonitorLogData( e.LogLevel | LogLevel.IsFiltered, finalTags, e.Text, CKException.CreateFrom( e.Exception ), e.FileName, e.LineNumber );
                                    d.SetExplicitLogTime( e.LogTime );
                                }
                                else d = default;
                                _monitor.UnfilteredOpenGroup( ref d );
                            }

                            break;
                        case LogEntryType.CloseGroup:
                            _monitor.CloseGroup( e.Conclusions, e.LogTime );
                            break;
                    }
                }
            }
            catch( Exception ex )
            {
                _endFlag = LogReceiverEndStatus.Error;
                _monitor.UnfilteredLog( LogLevel.Fatal, null, "While receiving pipe logs.", ex );
            }
        }

        /// <summary>
        /// Starts a receiver.
        /// <para>
        /// Its <see cref="PipeName"/> must be given to the <see cref="SimplePipeSenderActivityMonitorClient"/>
        /// (typically with a /logpipe: argument for the launched process) and <see cref="WaitEnd(bool)"/> should be
        /// called before disposing it.
        /// </para>
        /// <para>
        /// Once the child process has been started, no more logs should be emitted in the local monitor: the internal thread
        /// will receive the logs from the external client and relay them into the local monitor.
        /// </para>
        /// </summary>
        /// <param name="localMonitor">The local monitor to which all collected logs will be injected.</param>
        /// <param name="interProcess">True when the client will be created in another process. False for an intra-process client (but why would you need this?).</param>
        /// <returns>A started receiver, ready to inject external logs into the local monitor.</returns>
        public static SimpleLogPipeReceiver Start( IActivityMonitor localMonitor, bool interProcess = true ) => new SimpleLogPipeReceiver( localMonitor, interProcess );

    }
}
