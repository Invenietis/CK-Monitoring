using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring
{
    internal class DispatcherSink : IGrandOutputSink
    {
        readonly BlockingCollection<GrandOutputEventInfo> _queue;
        readonly Task _task;
        readonly List<IGrandOutputHandler> _handlers;
        readonly long _deltaExternalTicks;
        readonly Action _externalOnTimer;
        readonly object _confTrigger;

        GrandOutputConfiguration[] _newConf;
        TimeSpan _timerDuration;
        long _deltaTicks;
        long _nextTicks;
        long _nextExternalTicks;
        volatile int _stopFlag;
        volatile bool _forceClose;

        public DispatcherSink( TimeSpan timerDuration, TimeSpan externalTimerDuration, Action externalTimer )
        {
            _queue = new BlockingCollection<GrandOutputEventInfo>();
            _handlers = new List<IGrandOutputHandler>();
            _task = new Task( Process, TaskCreationOptions.LongRunning );
            _confTrigger = new object();
            _timerDuration = timerDuration;
            _deltaTicks = timerDuration.Ticks;
            _deltaExternalTicks = externalTimerDuration.Ticks;
            _externalOnTimer = externalTimer;
            long now = DateTime.UtcNow.Ticks;
            _nextTicks = now + timerDuration.Ticks;
            _nextExternalTicks = now + externalTimerDuration.Ticks;
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
            IActivityMonitor monitor = new SystemActivityMonitor( applyAutoConfigurations: false, topic: GetType().FullName );
            while( !_queue.IsCompleted && !_forceClose )
            {
                bool hasEvent = _queue.TryTake( out GrandOutputEventInfo e, millisecondsTimeout: 100 );
                var newConf = _newConf;
                if( newConf != null && newConf.Length > 0 )
                {
                    Util.InterlockedSet( ref _newConf, t => t.Skip( newConf.Length ).ToArray() );
                    var c = newConf[newConf.Length - 1];
                    TimerDuration = c.TimerDuration;
                    List<IGrandOutputHandler> toKeep = new List<IGrandOutputHandler>();
                    for( int iConf = 0; iConf < c.Handlers.Count; ++iConf )
                    {
                        for( int iHandler = 0; iHandler < _handlers.Count; ++iHandler )
                        {
                            if( _handlers[iHandler].ApplyConfiguration( monitor, c.Handlers[iConf] ) )
                            {
                                c.Handlers.RemoveAt( iConf-- );
                                toKeep.Add( _handlers[iHandler] );
                                _handlers.RemoveAt( iHandler );
                                break;
                            }
                        }
                    }
                    foreach( var h in _handlers )
                    {
                        SafeActivateOrDeactivate( monitor, h, false );
                    }
                    _handlers.Clear();
                    _handlers.AddRange( toKeep );
                    foreach( var conf in c.Handlers )
                    {
                        var h = GrandOutput.CreateHandler( conf );
                        if( SafeActivateOrDeactivate( monitor, h, true ) )
                        {
                            _handlers.Add( h );
                        }
                    }
                    lock( _confTrigger )
                        Monitor.PulseAll( _confTrigger );
                }
                List<IGrandOutputHandler> faulty = null;
                #region Process event if any.
                if( hasEvent )
                {
                    foreach( var h in _handlers )
                    {
                        try
                        {
                            h.Handle( e );
                        }
                        catch( Exception ex )
                        {
                            monitor.SendLine( LogLevel.Error, h.GetType().FullName + ".Handle", ex );
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
                            h.OnTimer( _timerDuration );
                        }
                        catch( Exception ex )
                        {
                            monitor.SendLine( LogLevel.Error, h.GetType().FullName + ".OnTimer", ex );
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
                    faulty = null;
                }
            }
            foreach( var h in _handlers ) SafeActivateOrDeactivate( monitor, h, false );
            monitor.MonitorEnd();
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
                monitor.SendLine( LogLevel.Error, h.GetType().FullName, ex );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts stopping this sink, returning true iif this call
        /// actually stopped it.
        /// </summary>
        /// <returns>
        /// True if this call stopped this sink, false if it has been already been stopped by another thread.
        /// </returns>
        public bool Stop()
        {
            if( Interlocked.Exchange( ref _stopFlag, 1 ) == 0 )
            {
                _queue.CompleteAdding();
                return true;
            }
            return false;
        }

        public void Finalize( int millisecondsBeforeForceClose )
        {
            if( !_task.Wait( millisecondsBeforeForceClose ) ) _forceClose = true;
            _task.Wait();
            _queue.Dispose();
        }

        public bool IsRunning => _stopFlag == 0;

        public void Handle( GrandOutputEventInfo logEvent ) => _queue.Add( logEvent );

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
    }
}
