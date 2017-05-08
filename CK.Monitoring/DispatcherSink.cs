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

        IGrandOutputHandler[] _toAdd;
        IGrandOutputHandler[] _toRemove;
        IGrandOutputHandler[] _toReplace;
        TimeSpan _timerDuration;
        long _deltaTicks;
        long _nextTicks;
        long _nextExternalTicks;
        bool _forceClose;

        public DispatcherSink(TimeSpan timerDuration, TimeSpan externalTimerDuration, Action externalTimer)
        {
            _queue = new BlockingCollection<GrandOutputEventInfo>();
            _handlers = new List<IGrandOutputHandler>();
            _task = new Task(Process, TaskCreationOptions.LongRunning);
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
            int nbEvent = 0;
            IActivityMonitor monitor = new SystemActivityMonitor(applyAutoConfigurations: false, topic: GetType().FullName);
            while (!_queue.IsCompleted || _forceClose)
            {
                bool hasEvent = _queue.TryTake(out GrandOutputEventInfo e, millisecondsTimeout: 250);
                #region Process handlers to Replace and Remove.
                var toReplace = _toReplace;
                if(toReplace != null)
                {
                    _toReplace = null;
                    foreach (var h in _handlers)
                    {
                        SafeActivateOrDeactivate(monitor, h, false);
                    }
                    _handlers.Clear();
                    _toRemove = null;
                    Util.InterlockedSet(ref _toAdd, t => t == null ? toReplace : t.Concat(toReplace).ToArray());
                }
                else
                {
                    var toRemove = _toRemove;
                    if (toRemove != null && toRemove.Length > 0)
                    {
                        foreach (var h in toRemove)
                        {
                            SafeActivateOrDeactivate(monitor, h, false);
                            _handlers.Remove(h);
                        }
                        Util.InterlockedRemoveAll(ref _toRemove, h => Array.IndexOf(toRemove, h) >= 0);
                    }
                }
                #endregion
                #region Process handlers to Add.
                var toAdd = _toAdd;
                if (toAdd != null && toAdd.Length > 0)
                {
                    foreach (var h in toAdd)
                    {
                        if (SafeActivateOrDeactivate(monitor, h, true))
                        {
                            _handlers.Add(h);
                        }
                    }
                    Util.InterlockedRemoveAll(ref _toAdd, h => Array.IndexOf(toAdd, h) >= 0);
                }
                #endregion
                #region Process event if any.
                if ( hasEvent )
                {
                    ++nbEvent;
                    foreach (var h in _handlers)
                    {
                        try
                        {
                            h.Handle(e);
                        }
                        catch (Exception ex)
                        {
                            monitor.SendLine(LogLevel.Error, h.GetType().FullName+".Handle", ex);
                            Util.InterlockedAdd(ref _toRemove, h);
                        }
                    }
                }
                #endregion
                #region Process OnTimer
                long now = DateTime.UtcNow.Ticks;
                if(now >= _nextTicks )
                {
                    foreach( var h in _handlers )
                    {
                        try
                        {
                            h.OnTimer( _timerDuration );
                        }
                        catch( Exception ex )
                        {
                            monitor.SendLine(LogLevel.Error, h.GetType().FullName + ".OnTimer", ex);
                            Util.InterlockedAdd(ref _toRemove, h);
                        }
                    }
                    _nextTicks = now + _deltaTicks;
                    if (now >= _nextExternalTicks)
                    {
                        _externalOnTimer();
                        _nextExternalTicks = now + _deltaExternalTicks;
                    }
                }
                #endregion
            }
            foreach (var h in _handlers) SafeActivateOrDeactivate(monitor, h, false);
            monitor.MonitorEnd();
        }

        bool SafeActivateOrDeactivate( IActivityMonitor monitor, IGrandOutputHandler h, bool activate )
        {
            try
            {
                if (activate) return h.Activate(monitor);
                else h.Deactivate(monitor);
            }
            catch (Exception ex)
            {
                monitor.SendLine(LogLevel.Error, h.GetType().FullName, ex);
                return false;
            }
            return true;
        }

        public void Stop(int millisecondsBeforeForceClose = 500)
        {
            _queue.CompleteAdding();
            if( !_task.Wait(millisecondsBeforeForceClose) ) _forceClose = true;
            _task.Wait();
        }

        public bool IsRunning => !_task.IsCompleted;

        public void Handle(GrandOutputEventInfo logEvent) => _queue.Add(logEvent);

        public void AddHandler(IGrandOutputHandler h) => Util.InterlockedAdd( ref _toAdd, h );

        public void RemoveHandler(IGrandOutputHandler h) => Util.InterlockedAdd( ref _toRemove, h );

        public void SetHandlers(IGrandOutputHandler[] handlers) => _toReplace = handlers;
    }
}
