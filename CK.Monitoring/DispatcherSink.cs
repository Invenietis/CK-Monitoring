using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring
{
    internal class DispatcherSink : IGrandOutputSink
    {
        readonly BlockingCollection<IMulticastLogEntry> _queue;
        readonly Task _task;
        readonly List<IGrandOutputHandler> _handlers;
        readonly long _deltaExternalTicks;
        readonly Action _externalOnTimer;
        readonly object _confTrigger;
        readonly Action<IActivityMonitor> _initialRegister;
        readonly Action<LogFilter?, LogLevelFilter?> _filterChange;
        readonly CancellationTokenSource _stopTokenSource;
        readonly object _externalLogLock;
        DateTimeStamp _externalLogLastTime;

        GrandOutputConfiguration[] _newConf;
        TimeSpan _timerDuration;
        long _deltaTicks;
        long _nextTicks;
        long _nextExternalTicks;
        int _configurationCount;
        volatile int _stopFlag;
        volatile bool _forceClose;
        readonly bool _isDefaultGrandOutput;
        bool _unhandledExceptionTracking;

        public DispatcherSink(
            Action<IActivityMonitor> initialRegister,
            TimeSpan timerDuration,
            TimeSpan externalTimerDuration,
            Action externalTimer,
            Action<LogFilter?,LogLevelFilter?> filterChange,
            bool isDefaultGrandOutput )
        {
            _initialRegister = initialRegister;
            _queue = new BlockingCollection<IMulticastLogEntry>();
            _handlers = new List<IGrandOutputHandler>();
            _task = new Task( Process, TaskCreationOptions.LongRunning );
            _confTrigger = new object();
            _stopTokenSource = new CancellationTokenSource();
            _timerDuration = timerDuration;
            _deltaTicks = timerDuration.Ticks;
            _deltaExternalTicks = externalTimerDuration.Ticks;
            _externalOnTimer = externalTimer;
            long now = DateTime.UtcNow.Ticks;
            _nextTicks = now + timerDuration.Ticks;
            _nextExternalTicks = now + externalTimerDuration.Ticks;
            _filterChange = filterChange;
            _externalLogLock = new object();
            _externalLogLastTime = DateTimeStamp.MinValue;
            _isDefaultGrandOutput = isDefaultGrandOutput;
            _newConf = Array.Empty<GrandOutputConfiguration>();

            _task.Start();
        }

        public TimeSpan TimerDuration
        {
            get => _timerDuration;
            set
            {
                if( _timerDuration != value )
                {
                    _timerDuration = value;
                    _deltaTicks = value.Ticks;
                }
            }
        }

        void Process()
        {
            var monitor = new ActivityMonitor( applyAutoConfigurations: false );
            // Simple pooling for initial configuration.
            GrandOutputConfiguration[] newConf = _newConf;
            while( newConf.Length == 0 )
            {
                Thread.Sleep( 0 );
                newConf = _newConf;
            }
            _initialRegister( monitor );
            monitor.SetTopic( "CK.Monitoring.DispatcherSink" );
            DoConfigure( monitor, newConf );
            while( !_queue.IsCompleted && !_forceClose )
            {
                bool hasEvent = _queue.TryTake( out var e, millisecondsTimeout: 100 );
                newConf = _newConf;
                Debug.Assert( newConf != null, "Except at the start, this is never null." );
                if( newConf.Length > 0 ) DoConfigure( monitor, newConf );
                List<IGrandOutputHandler>? faulty = null;
                #region Process event if any.
                if( hasEvent )
                {
                    Debug.Assert( e != null );
                    foreach( var h in _handlers )
                    {
                        try
                        {
                            h.Handle( monitor, e );
                        }
                        catch( Exception ex )
                        {
                            monitor.Fatal( $"{h.GetType()}.Handle() crashed.", ex );
                            if( faulty == null ) faulty = new List<IGrandOutputHandler>();
                            faulty.Add( h );
                        }
                    }
                }
                #endregion
                #region Process OnTimer
                long now = DateTime.UtcNow.Ticks;
                if( now >= _nextTicks )
                {
                    foreach( var h in _handlers )
                    {
                        try
                        {
                            h.OnTimer( monitor, _timerDuration );
                        }
                        catch( Exception ex )
                        {
                            monitor.Fatal( $"{h.GetType()}.OnTimer() crashed.", ex );
                            if( faulty == null ) faulty = new List<IGrandOutputHandler>();
                            faulty.Add( h );
                        }
                    }
                    _nextTicks = now + _deltaTicks;
                    if( now >= _nextExternalTicks )
                    {
                        _externalOnTimer();
                        _nextExternalTicks = now + _deltaExternalTicks;
                    }
                }
                #endregion
                if( faulty != null )
                {
                    foreach( var h in faulty )
                    {
                        SafeActivateOrDeactivate( monitor, h, false );
                        _handlers.Remove( h );
                    }
                }
            }
            foreach( var h in _handlers ) SafeActivateOrDeactivate( monitor, h, false );
            monitor.MonitorEnd();
        }

        void DoConfigure( IActivityMonitor monitor, GrandOutputConfiguration[] newConf )
        {
            Util.InterlockedSet( ref _newConf, t => t.Skip( newConf.Length ).ToArray() );
            var c = newConf[newConf.Length - 1];    
            _filterChange( c.MinimalFilter, c.ExternalLogLevelFilter );
            if( c.TimerDuration.HasValue ) TimerDuration = c.TimerDuration.Value;
            SetUnhandledExceptionTracking( c.TrackUnhandledExceptions ?? _isDefaultGrandOutput );
            List<IGrandOutputHandler> toKeep = new List<IGrandOutputHandler>();
            for( int iConf = 0; iConf < c.Handlers.Count; ++iConf )
            {
                for( int iHandler = 0; iHandler < _handlers.Count; ++iHandler )
                {
                    try
                    {
                        if( _handlers[iHandler].ApplyConfiguration( monitor, c.Handlers[iConf] ) )
                        {
                            // Existing _handlers[iHandler] accepted the new c.Handlers[iConf].
                            c.Handlers.RemoveAt( iConf-- );
                            toKeep.Add( _handlers[iHandler] );
                            _handlers.RemoveAt( iHandler );
                            break;
                        }
                    }
                    catch( Exception ex )
                    {
                        var h = _handlers[iHandler];
                        // Existing _handlers[iHandler] crashed with the proposed c.Handlers[iConf].
                        monitor.Fatal( $"Existing {h.GetType()} crashed with the configuration {c.Handlers[iConf].GetType()}.", ex );
                        // Since the handler can be compromised, we skip it from any subsequent
                        // attempt to reconfigure it and deactivate it.
                        _handlers.RemoveAt( iHandler-- );
                        SafeActivateOrDeactivate( monitor, h, false );
                    }
                }
            }
            // Deactivate and get rid of remaining handlers.
            foreach( var h in _handlers )
            {
                SafeActivateOrDeactivate( monitor, h, false );
            }
            _handlers.Clear();
            // Restores reconfigured handlers.
            _handlers.AddRange( toKeep );
            // Creates and activates new handlers.
            foreach( var conf in c.Handlers )
            {
                try
                {
                    var h = GrandOutput.CreateHandler( conf );
                    if( SafeActivateOrDeactivate( monitor, h, true ) )
                    {
                        _handlers.Add( h );
                    }
                }
                catch( Exception ex )
                {
                    monitor.Fatal( $"While creating handler for {conf.GetType()}.", ex );
                }
            }
            if( _isDefaultGrandOutput )
            {
                // No need to Dispose() this Process.
                var thisProcess = System.Diagnostics.Process.GetCurrentProcess();
                var msg = $"GrandOutput.Default configuration n°{_configurationCount++} for '{thisProcess.ProcessName}' (PID:{thisProcess.Id},AppDomainId:{AppDomain.CurrentDomain.Id}) on machine {Environment.MachineName}, UserName: '{Environment.UserName}', CommandLine: '{Environment.CommandLine}', BaseDirectory: '{AppContext.BaseDirectory}'.";
                ExternalLog( LogLevel.Info | LogLevel.IsFiltered, ActivityMonitor.Tags.Empty, msg, null );
            }
            lock( _confTrigger )
                Monitor.PulseAll( _confTrigger );
        }

        bool SafeActivateOrDeactivate( IActivityMonitor monitor, IGrandOutputHandler h, bool activate )
        {
            try
            {
                if( activate ) return h.Activate( monitor );
                else h.Deactivate( monitor );
            }
            catch( Exception ex )
            {
                monitor.Fatal( $"Handler {h.GetType()} crashed during {(activate ? "activation" : "de-activation")}.", ex );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets a cancellation token that is canceled by Stop.
        /// </summary>
        public CancellationToken StoppingToken => _stopTokenSource.Token;

        /// <summary>
        /// Starts stopping this sink, returning true if and only if this call
        /// actually stopped it.
        /// </summary>
        /// <returns>
        /// True if this call stopped this sink, false if it has been already been stopped by another thread.
        /// </returns>
        public bool Stop()
        {
            if( Interlocked.Exchange( ref _stopFlag, 1 ) == 0 )
            {
                SetUnhandledExceptionTracking( false );
                _stopTokenSource.Cancel();
                _queue.CompleteAdding();
                return true;
            }
            return false;
        }

        public void Finalize( int millisecondsBeforeForceClose )
        {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            if( !_task.Wait( millisecondsBeforeForceClose ) ) _forceClose = true;
            _task.Wait();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            _queue.Dispose();
            _stopTokenSource.Dispose();
        }

        public bool IsRunning => _stopFlag == 0;

        public void Handle( IMulticastLogEntry logEvent )
        {
            if( _stopFlag == 0 ) _queue.Add( logEvent );
        }

        public void ApplyConfiguration( GrandOutputConfiguration configuration, bool waitForApplication )
        {
            Debug.Assert( configuration.InternalClone );
            Util.InterlockedAdd( ref _newConf, configuration );
            if( waitForApplication )
            {
                lock( _confTrigger )
                {
                    GrandOutputConfiguration[] newConf;
                    while( _stopFlag == 0 && (newConf = _newConf) != null && newConf.Contains( configuration ) )
                        Monitor.Wait( _confTrigger );
                }
            }
        }

        public void ExternalLog( LogLevel level, CKTrait? tags, string message, Exception? ex )
        {
            DateTimeStamp prevLogTime;
            DateTimeStamp logTime;
            lock( _externalLogLock )
            {
                prevLogTime = _externalLogLastTime;
                _externalLogLastTime = logTime = new DateTimeStamp( _externalLogLastTime, DateTime.UtcNow );
            }
            var e = LogEntry.CreateMulticastLog( GrandOutput.ExternalLogMonitorUniqueId,
                                                 LogEntryType.Line,
                                                 prevLogTime,
                                                 depth: 0,
                                                 text: string.IsNullOrEmpty( message ) ? ActivityMonitor.NoLogText : message,
                                                 t: logTime,
                                                 level: level,
                                                 fileName: null,
                                                 lineNumber: 0,
                                                 tags: tags ?? ActivityMonitor.Tags.Empty,
                                                 ex: ex != null ? CKExceptionData.CreateFrom( ex ) : null );
            Handle( e );
        }

        public void ExternalLog( ref ActivityMonitorLogData d )
        {
            DateTimeStamp prevLogTime;
            DateTimeStamp logTime;
            lock( _externalLogLock )
            {
                prevLogTime = _externalLogLastTime;
                _externalLogLastTime = logTime = new DateTimeStamp( _externalLogLastTime, DateTime.UtcNow );
            }
            var e = LogEntry.CreateMulticastLog( GrandOutput.ExternalLogMonitorUniqueId,
                                                 LogEntryType.Line,
                                                 prevLogTime,
                                                 depth: 0,
                                                 d.Text,
                                                 logTime,
                                                 d.Level,
                                                 d.FileName,
                                                 d.LineNumber,
                                                 d.Tags,
                                                 d.ExceptionData );
            Handle( e );
        }

        void SetUnhandledExceptionTracking( bool trackUnhandledException )
        {
            if( trackUnhandledException != _unhandledExceptionTracking )
            {
                if( trackUnhandledException )
                {
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
                }
                else
                {
                    AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                    TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
                }
                _unhandledExceptionTracking = trackUnhandledException;
            }
        }

        void OnUnobservedTaskException( object? sender, UnobservedTaskExceptionEventArgs e )
        {
            ExternalLog( LogLevel.Fatal, GrandOutput.UnhandledException, "TaskScheduler.UnobservedTaskException raised.", e.Exception );
            e.SetObserved();
        }

        void OnUnhandledException( object sender, UnhandledExceptionEventArgs e )
        {
            if( e.ExceptionObject is Exception ex )
            {
                ExternalLog( LogLevel.Fatal, GrandOutput.UnhandledException, "AppDomain.CurrentDomain.UnhandledException raised.", ex );
            }
            else
            {
                string? exText = e.ExceptionObject.ToString();
                ExternalLog( LogLevel.Fatal, GrandOutput.UnhandledException, $"AppDomain.CurrentDomain.UnhandledException raised with Exception object '{exText}'.", null );
            }
        }



    }
}
