using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Monitoring
{
    internal class DispatcherSink : IGrandOutputSink
    {
        readonly Channel<IMulticastLogEntry?> _queue;

        readonly Task _task;
        readonly List<IGrandOutputHandler> _handlers;
        readonly IdentityCard _identityCard;
        readonly long _deltaExternalTicks;
        readonly Action _externalOnTimer;
        readonly object _confTrigger;
        readonly Action<IActivityMonitor> _initialRegister;
        readonly Action<LogFilter?, LogLevelFilter?> _filterChange;
        readonly CancellationTokenSource _stopTokenSource;
        readonly object _externalLogLock;
        string? _sinkMonitorId;

        GrandOutputConfiguration[] _newConf;
        TimeSpan _timerDuration;
        long _deltaTicks;
        long _nextTicks;
        long _nextExternalTicks;
        int _configurationCount;
        DateTimeStamp _externalLogLastTime;
        volatile bool _forceClose;
        readonly bool _isDefaultGrandOutput;
        bool _unhandledExceptionTracking;

        public DispatcherSink( Action<IActivityMonitor> initialRegister,
                               IdentityCard identityCard,
                               TimeSpan timerDuration,
                               TimeSpan externalTimerDuration,
                               Action externalTimer,
                               Action<LogFilter?, LogLevelFilter?> filterChange,
                               bool isDefaultGrandOutput )
        {
            _initialRegister = initialRegister;
            _identityCard = identityCard;
            _queue = Channel.CreateUnbounded<IMulticastLogEntry?>( new UnboundedChannelOptions() { SingleReader = true } );
            _handlers = new List<IGrandOutputHandler>();
            _confTrigger = new object();
            _stopTokenSource = new CancellationTokenSource();
            _timerDuration = timerDuration;
            _deltaTicks = timerDuration.Ticks;
            _deltaExternalTicks = externalTimerDuration.Ticks;
            _externalOnTimer = externalTimer;
            _filterChange = filterChange;
            _externalLogLock = new object();
            _externalLogLastTime = DateTimeStamp.MinValue;
            _isDefaultGrandOutput = isDefaultGrandOutput;
            _newConf = Array.Empty<GrandOutputConfiguration>();
            _task = ProcessAsync();
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

        async Task ProcessAsync()
        {
            // We emit the identity card changed from this monitor.
            var monitor = new ActivityMonitor( applyAutoConfigurations: false );
            _sinkMonitorId = monitor.UniqueId;
            // Simple pooling for initial configuration.
            // Starting with the delay avoids a Task.Run() is the constructor.
            GrandOutputConfiguration[] newConf = _newConf;
            do
            {
                await Task.Delay( 5 );
                newConf = _newConf;
            }
            while( newConf.Length == 0 );
            // The initial configuration is available. Registers our loop monitor
            // and applies the configuration.
            _initialRegister( monitor );
            monitor.SetTopic( "CK.Monitoring.DispatcherSink" );
            await DoConfigureAsync( monitor, newConf );
            // Initialize the identity card.
            _identityCard.LocalInitialize( monitor, _isDefaultGrandOutput );
            // First register to the OnChange to avoid missing an update...
            _identityCard.OnChanged += IdentityCardOnChanged;
            // ...then sends the current content of the identity card.
            monitor.UnfilteredLog( LogLevel.Info | LogLevel.IsFiltered, IdentityCard.Tags.IdentityCard, _identityCard.ToString(), null );
            // Configures the next timer due date.
            long now = DateTime.UtcNow.Ticks;
            _nextTicks = now + _timerDuration.Ticks;
            _nextExternalTicks = now + _timerDuration.Ticks;
            // Creates and launch the "awaker". This avoids any CancellationToken.
            Timer awaker = new Timer( _ => _queue.Writer.TryWrite( null ), null, 100, 100 );
            while( !_forceClose && await _queue.Reader.WaitToReadAsync() )
            {
                _queue.Reader.TryRead( out var e );
                newConf = _newConf;
                Debug.Assert( newConf != null, "Except at the start, this is never null." );
                if( newConf.Length > 0 ) await DoConfigureAsync( monitor, newConf );
                List<IGrandOutputHandler>? faulty = null;
                #region Process event if any.
                if( e != null )
                {
                    foreach( var h in _handlers )
                    {
                        try
                        {
                            await h.HandleAsync( monitor, e );
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
                #region Process OnTimer (if not closing)
                if( !_stopTokenSource.IsCancellationRequested )
                {
                    now = DateTime.UtcNow.Ticks;
                    if( now >= _nextTicks )
                    {
                        foreach( var h in _handlers )
                        {
                            try
                            {
                                await h.OnTimerAsync( monitor, _timerDuration );
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
                }
                #endregion
                if( faulty != null )
                {
                    foreach( var h in faulty )
                    {
                        await SafeActivateOrDeactivateAsync( monitor, h, false );
                        _handlers.Remove( h );
                    }
                }
            }
            await awaker.DisposeAsync();
            foreach( var h in _handlers ) await SafeActivateOrDeactivateAsync( monitor, h, false );
            monitor.MonitorEnd();
        }

        void IdentityCardOnChanged( IdentiCardChangedEvent obj )
        {
            Debug.Assert( _sinkMonitorId != null );
            ExternalLog( LogLevel.Info | LogLevel.IsFiltered, IdentityCard.Tags.IdentityCardAdd, _identityCard.ToString(), null, _sinkMonitorId );
        }

        void OnAwaker( object? state ) => _queue.Writer.TryWrite( null );

        async ValueTask DoConfigureAsync( IActivityMonitor monitor, GrandOutputConfiguration[] newConf )
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
                        if( await _handlers[iHandler].ApplyConfigurationAsync( monitor, c.Handlers[iConf] ) )
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
                        await SafeActivateOrDeactivateAsync( monitor, h, false );
                    }
                }
            }
            // Deactivate and get rid of remaining handlers.
            foreach( var h in _handlers )
            {
                await SafeActivateOrDeactivateAsync( monitor, h, false );
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
                    if( await SafeActivateOrDeactivateAsync( monitor, h, true ) )
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
                var msg = $"GrandOutput.Default configuration n°{_configurationCount++}.";
                ExternalLog( LogLevel.Info | LogLevel.IsFiltered, ActivityMonitor.Tags.Empty, msg, null );
            }
            lock( _confTrigger )
                Monitor.PulseAll( _confTrigger );
        }

        async ValueTask<bool> SafeActivateOrDeactivateAsync( IActivityMonitor monitor, IGrandOutputHandler h, bool activate )
        {
            try
            {
                if( activate ) return await h.ActivateAsync( monitor );
                else await h.DeactivateAsync( monitor );
                return true;
            }
            catch( Exception ex )
            {
                monitor.Fatal( $"Handler {h.GetType()} crashed during {(activate ? "activation" : "de-activation")}.", ex );
                return false;
            }
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
            if( _queue.Writer.TryComplete() )
            {
                _identityCard.LocalUninitialize( _isDefaultGrandOutput );
                SetUnhandledExceptionTracking( false );
                _stopTokenSource.Cancel();
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
            _stopTokenSource.Dispose();
        }

        public bool IsRunning => !_stopTokenSource.IsCancellationRequested;

        public void Handle( IMulticastLogEntry logEvent ) => _queue.Writer.TryWrite( logEvent );

        public void ApplyConfiguration( GrandOutputConfiguration configuration, bool waitForApplication )
        {
            Debug.Assert( configuration.InternalClone );
            Util.InterlockedAdd( ref _newConf, configuration );
            if( waitForApplication )
            {
                lock( _confTrigger )
                {
                    GrandOutputConfiguration[] newConf;
                    while( !_stopTokenSource.IsCancellationRequested && (newConf = _newConf) != null && newConf.Contains( configuration ) )
                        Monitor.Wait( _confTrigger );
                }
            }
        }

        public void ExternalLog( LogLevel level, CKTrait? tags, string message, Exception? ex, string monitorId = GrandOutput.ExternalLogMonitorUniqueId )
        {
            DateTimeStamp prevLogTime;
            DateTimeStamp logTime;
            lock( _externalLogLock )
            {
                prevLogTime = _externalLogLastTime;
                _externalLogLastTime = logTime = new DateTimeStamp( _externalLogLastTime, DateTime.UtcNow );
            }
            var e = LogEntry.CreateMulticastLog( monitorId,
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
