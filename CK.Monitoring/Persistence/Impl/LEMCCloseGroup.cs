using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Monitoring.Impl
{
    sealed class LEMCCloseGroup : LECloseGroup, IMulticastLogEntry
    {
        readonly string _monitorId;
        readonly string _grandOutputId;
        readonly int _depth;
        readonly DateTimeStamp _previousLogTime;
        readonly LogEntryType _previousEntryType;

        public LEMCCloseGroup( string grandOutputId,
                               string monitorId,
                               int depth,
                               DateTimeStamp previousLogTime,
                               LogEntryType previousEntryType,
                               DateTimeStamp t,
                               LogLevel level,
                               IReadOnlyList<ActivityLogGroupConclusion>? c )
            : base( t, level, c )
        {
            _grandOutputId = grandOutputId;
            _monitorId = monitorId;
            _depth = depth;
            _previousEntryType = previousEntryType;
            _previousLogTime = previousLogTime;
        }

        public string GrandOutputId => _grandOutputId;

        public string MonitorId => _monitorId;

        public int GroupDepth => _depth;

        public DateTimeStamp PreviousLogTime => _previousLogTime; 

        public LogEntryType PreviousEntryType => _previousEntryType; 

        public override void WriteLogEntry( CKBinaryWriter w )
        {
            LogEntry.WriteCloseGroup( w, _grandOutputId, _monitorId, _previousEntryType, _previousLogTime, _depth, LogLevel, LogTime, Conclusions );
        }

        public ILogEntry CreateUnicastLogEntry()
        {
            return new LECloseGroup( this );
        }

    }
}
