using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        IActivityMonitorImpl _monitorSource;

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
        void IActivityMonitorBoundClient.SetMonitor( IActivityMonitorImpl source, bool forceBuggyRemove )
        {
            if( source != null && _monitorSource != null ) throw ActivityMonitorClient.CreateMultipleRegisterOnBoundClientException( this );
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

        void IActivityMonitorClient.OnTopicChanged( string newTopic, string fileName, int lineNumber )
        {
        }

        LogFilter IActivityMonitorBoundClient.MinimalFilter => LogFilter.Undefined;

        internal bool IsBoundToMonitor => _monitorSource != null; 

        void IActivityMonitorClient.OnUnfilteredLog( ActivityMonitorLogData data )
        {
            IMulticastLogEntry e = LogEntry.CreateMulticastLog( _monitorSource.UniqueId, _prevLogType, _prevlogTime, _currentGroupDepth, data.Text, data.LogTime, data.Level, data.FileName, data.LineNumber, data.Tags, data.EnsureExceptionData() );
            _central.Sink.Handle( new GrandOutputEventInfo( e, _monitorSource.Topic ) );
            _prevlogTime = data.LogTime;
            _prevLogType = LogEntryType.Line;
        }

        void IActivityMonitorClient.OnOpenGroup( IActivityLogGroup group )
        {
            IMulticastLogEntry e = LogEntry.CreateMulticastOpenGroup( _monitorSource.UniqueId, _prevLogType, _prevlogTime, _currentGroupDepth, group.GroupText, group.LogTime, group.GroupLevel, group.FileName, group.LineNumber, group.GroupTags, group.EnsureExceptionData() );
            _central.Sink.Handle( new GrandOutputEventInfo( e, _monitorSource.Topic ) );
            ++_currentGroupDepth;
            _prevlogTime = group.LogTime;
            _prevLogType = LogEntryType.OpenGroup;
        }

        void IActivityMonitorClient.OnGroupClosing( IActivityLogGroup group, ref List<ActivityLogGroupConclusion> conclusions )
        {
        }

        void IActivityMonitorClient.OnGroupClosed( IActivityLogGroup group, IReadOnlyList<ActivityLogGroupConclusion> conclusions )
        {
            IMulticastLogEntry e = LogEntry.CreateMulticastCloseGroup( _monitorSource.UniqueId, _prevLogType, _prevlogTime, _currentGroupDepth, group.CloseLogTime, group.GroupLevel, conclusions );
            _central.Sink.Handle( new GrandOutputEventInfo( e, _monitorSource.Topic ) );
            --_currentGroupDepth;
            _prevlogTime = group.CloseLogTime;
            _prevLogType = LogEntryType.CloseGroup;
        }

        void IActivityMonitorClient.OnAutoTagsChanged( CKTrait newTrait )
        {
        }
    }
}
