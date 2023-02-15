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
    internal partial class DispatcherSink : IGrandOutputSink
    {
        readonly Channel<InputLogEntry?> _queue;

        readonly Task _task;
        readonly List<IGrandOutputHandler> _handlers;
        readonly IdentityCard _identityCard;
        readonly long _deltaExternalTicks;
        readonly Action _externalOnTimer;
        readonly object _confTrigger;
        readonly Action<IActivityMonitor> _initialRegister;
        readonly Action<LogFilter?,LogLevelFilter?> _filterChange;
        readonly CancellationTokenSource _stopTokenSource;
        readonly object _externalLogLock;
        readonly string _sinkMonitorId;

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
                               Action<LogFilter?,LogLevelFilter?> filterChange,
                               bool isDefaultGrandOutput )
        {
            _initialRegister = initialRegister;
            _identityCard = identityCard;
            _queue = Channel.CreateUnbounded<InputLogEntry?>( new UnboundedChannelOptions() { SingleReader = true } );
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
            var monitor = new ActivityMonitor( applyAutoConfigurations: false );
            // We emit the identity card changed from this monitor (so we need its id).
            // But more importantly, this monitor identifier is the one of the GrandOutput: each log entry
            // references this identifier.
            _sinkMonitorId = monitor.UniqueId;
            _task = ProcessAsync( monitor );
        }

        public string SinkMonitorId => _sinkMonitorId;

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

        async Task ProcessAsync( IActivityMonitor monitor )
        {
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
            monitor.UnfilteredLog( LogLevel.Info | LogLevel.IsFiltered, IdentityCard.Tags.IdentityCardFull, _identityCard.ToString(), null );
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
                #region Process event if any (including the CloseSentinel).
                if( e != null )
                {
                    // The CloseSentinel is the "soft stop": it ensures that any entries added prior
                    // to the call to stop have been handled (but if _forceClose is set, this is ignored).
                    if( e == InputLogEntry.CloseSentinel ) break;
                    // Regular handling.
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
                    e.Release();
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
            // Whether we are in _forceClose or not, release any remaining entries that may
            // have been written to the channel.
            while( _queue.Reader.TryRead( out var more ) )
            {
                // Do NOT handle these entries!
                // This GrandOuput/Sink is closed, handling them would be too risky
                // and semantically questionable.
                // We only release the entries that have been written to the defunct channel.
                if( more != null && more != InputLogEntry.CloseSentinel )
                {
                    more.Release();
                }
            }
            foreach( var h in _handlers ) await SafeActivateOrDeactivateAsync( monitor, h, false );
            monitor.MonitorEnd();
        }

        void IdentityCardOnChanged( IdentiCardChangedEvent change )
        {
            ExternalLog( LogLevel.Info | LogLevel.IsFiltered, IdentityCard.Tags.IdentityCardUpdate, change.PackedAddedInfo, null, _sinkMonitorId );
        }

        async ValueTask DoConfigureAsync( IActivityMonitor monitor, GrandOutputConfiguration[] newConf )
        {
            Util.InterlockedSet( ref _newConf, t => t.Skip( newConf.Length ).ToArray() );
            var c = newConf[newConf.Length - 1];    
            _filterChange( c.MinimalFilter, c.ExternalLogLevelFilter );
            if( c.TimerDuration.HasValue ) TimerDuration = c.TimerDuration.Value;
            SetUnhandledExceptionTracking( c.TrackUnhandledExceptions ?? _isDefaultGrandOutput );
            if( !string.IsNullOrEmpty( c.StaticGates ) ) StaticGateConfigurator.ApplyConfiguration( monitor, c.StaticGates );
            if( !string.IsNullOrEmpty( c.DotNetEventSources ) ) DotNetEventSourceConfigurator.ApplyConfiguration( monitor, c.DotNetEventSources );
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
            if( _handlers.Count > 0 )
            {
                foreach( var h in _handlers )
                {
                    await SafeActivateOrDeactivateAsync( monitor, h, false );
                }
            }
            _handlers.Clear();
            // Restores reconfigured handlers.
            _handlers.AddRange( toKeep );
            // Creates and activates new handlers.
            // Rather than handling a special case for the IdentityCard, we use
            // a service provider to be able to extend the services one day.
            if( c.Handlers.Count > 0 )
            {
                using( var container = new SimpleServiceContainer() )
                {
                    container.Add( _identityCard );
                    foreach( var conf in c.Handlers )
                    {
                        try
                        {
                            var h = GrandOutput.CreateHandler( conf, container );
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
                }
            }
            if( _isDefaultGrandOutput )
            {
                ExternalLog( LogLevel.Info | LogLevel.IsFiltered, ActivityMonitor.Tags.Empty, $"GrandOutput.Default configuration n°{_configurationCount++}." );
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
            // TryWrite and TryComplete return false if the channel has been completed.
            if( _queue.Writer.TryWrite( InputLogEntry.CloseSentinel ) && _queue.Writer.TryComplete() )
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

        public void Handle( InputLogEntry logEvent )
        {
            // If we cannot write the entry, we must release it right now.
            // A race condition may appear here: Stop() calls TryWrite( CloseSentinel ) && TryComplete(),
            // and we may be here between the 2 calls which means that regular entries have been written
            // after the CloseSentinel. This is why the handling of the CloseSentinel drains any remaining
            // entries before leaving ProcessAsync.
            if( !_queue.Writer.TryWrite( logEvent ) )
            {
                logEvent.Release();
            }
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
                    while( !_stopTokenSource.IsCancellationRequested && (newConf = _newConf) != null && newConf.Contains( configuration ) )
                        Monitor.Wait( _confTrigger );
                }
            }
        }

        public void ExternalLog( LogLevel level, CKTrait? tags, string message, Exception? ex = null, string monitorId = GrandOutput.ExternalLogMonitorUniqueId )
        {
            DateTimeStamp prevLogTime;
            DateTimeStamp logTime;
            lock( _externalLogLock )
            {
                prevLogTime = _externalLogLastTime;
                _externalLogLastTime = logTime = new DateTimeStamp( _externalLogLastTime, DateTime.UtcNow );
            }
            var e = InputLogEntry.AcquireInputLogEntry( _sinkMonitorId,
                                                        monitorId,
                                                        prevLogTime,
                                                        string.IsNullOrEmpty( message ) ? ActivityMonitor.NoLogText : message,
                                                        logTime,
                                                        level,
                                                        tags ?? ActivityMonitor.Tags.Empty,
                                                        CKExceptionData.CreateFrom( ex ) );
            Handle( e );
        }

        public void OnStaticLog( ref ActivityMonitorLogData d )
        {
            DateTimeStamp prevLogTime;
            DateTimeStamp logTime;
            lock( _externalLogLock )
            {
                prevLogTime = _externalLogLastTime;
                _externalLogLastTime = logTime = new DateTimeStamp( _externalLogLastTime, DateTime.UtcNow );
            }
            var e = InputLogEntry.AcquireInputLogEntry( _sinkMonitorId,
                                                        ref d,
                                                        groupDepth: 0,
                                                        LogEntryType.Line,
                                                        GrandOutput.ExternalLogMonitorUniqueId,
                                                        LogEntryType.Line,
                                                        prevLogTime );
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
                ExternalLog( LogLevel.Fatal, GrandOutput.UnhandledException, $"AppDomain.CurrentDomain.UnhandledException raised with Exception object '{exText}'." );
            }
        }
    }
}
