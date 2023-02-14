using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;
using CK.Core.Impl;

namespace CK.Monitoring
{

    /// <summary>
    /// A GrandOutputClient is a <see cref="IActivityMonitorClient"/> that can only be obtained and registered
    /// through <see cref="GrandOutput.EnsureGrandOutputClient(IActivityMonitor)"/>.
    /// </summary>
    public sealed class GrandOutputClient : IActivityMonitorBoundClient
    {
        readonly GrandOutput _central;
        IActivityMonitorImpl? _monitorSource;

        int _currentGroupDepth;
        LogEntryType _prevLogType;
        DateTimeStamp _prevlogTime;

        internal GrandOutputClient( GrandOutput central )
        {
            _central = central;
        }

        /// <summary>
        /// forceBuggyRemove is not used here since this client is not lockable.
        /// </summary>
        void IActivityMonitorBoundClient.SetMonitor( IActivityMonitorImpl? source, bool forceBuggyRemove )
        {
            if( source != null && _monitorSource != null ) ActivityMonitorClient.ThrowMultipleRegisterOnBoundClientException( this );
            // Silently ignore null => null or monitor => same monitor.
            if( source != _monitorSource )
            {
                _prevLogType = LogEntryType.None;
                _prevlogTime = DateTimeStamp.Unknown;
                Debug.Assert( (source == null) != (_monitorSource == null) );
                if( (_monitorSource = source) != null )
                {
                    var g = _monitorSource.CurrentGroup;
                    _currentGroupDepth = g != null ? g.Depth : 0;
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="GrandOutput"/> to which this <see cref="GrandOutputClient"/> is bound.
        /// </summary>
        public GrandOutput Central => _central; 

        void IActivityMonitorClient.OnTopicChanged( string newTopic, string? fileName, int lineNumber )
        {
        }

        LogFilter IActivityMonitorBoundClient.MinimalFilter => _central.MinimalFilter;

        bool IActivityMonitorBoundClient.IsDead => _central.IsDisposed;

        internal bool IsBoundToMonitor => _monitorSource != null; 

        internal void OnGrandOutputDisposedOrMinimalFilterChanged() => _monitorSource?.SignalChange();
        
        void IActivityMonitorClient.OnUnfilteredLog( ref ActivityMonitorLogData data )
        {
            if( _central.IsDisposed ) return;
            Debug.Assert( _monitorSource != null, "Since we are called by the monitor..." );
            InputLogEntry e = InputLogEntry.AcquireInputLogEntry( _central.GrandOutpuId,
                                                                  ref data,
                                                                  _currentGroupDepth,
                                                                  LogEntryType.Line,
                                                                  _monitorSource.UniqueId,
                                                                  _prevLogType,
                                                                  _prevlogTime );
            _central.Sink.Handle( e );
            _prevlogTime = data.LogTime;
            _prevLogType = LogEntryType.Line;
        }

        void IActivityMonitorClient.OnOpenGroup( IActivityLogGroup group )
        {
            if( _central.IsDisposed ) return;
            Debug.Assert( _monitorSource != null, "Since we are called by the monitor..." );
            InputLogEntry e = InputLogEntry.AcquireInputLogEntry( _central.GrandOutpuId,
                                                                  ref group.Data,
                                                                  _currentGroupDepth,
                                                                  LogEntryType.OpenGroup,
                                                                  _monitorSource.UniqueId,
                                                                  _prevLogType,
                                                                  _prevlogTime );
            _central.Sink.Handle( e );
            ++_currentGroupDepth;
            _prevlogTime = group.Data.LogTime;
            _prevLogType = LogEntryType.OpenGroup;
        }

        void IActivityMonitorClient.OnGroupClosing( IActivityLogGroup group, ref List<ActivityLogGroupConclusion>? conclusions )
        {
        }

        void IActivityMonitorClient.OnGroupClosed( IActivityLogGroup group, IReadOnlyList<ActivityLogGroupConclusion> conclusions )
        {
            if( _central.IsDisposed ) return;
            Debug.Assert( _monitorSource != null, "Since we are called by the monitor..." );
            InputLogEntry e = InputLogEntry.AcquireInputLogEntry( _central.GrandOutpuId,
                                                                  group.CloseLogTime,
                                                                  conclusions,
                                                                  group.Data.Level,
                                                                  _currentGroupDepth,
                                                                  _monitorSource.UniqueId,
                                                                  _prevLogType,
                                                                  _prevlogTime );
            _central.Sink.Handle( e );
            --_currentGroupDepth;
            _prevlogTime = group.CloseLogTime;
            _prevLogType = LogEntryType.CloseGroup;
        }

        void IActivityMonitorClient.OnAutoTagsChanged( CKTrait newTrait )
        {
        }
    }
}
