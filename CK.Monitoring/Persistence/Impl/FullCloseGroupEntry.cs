using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Monitoring.Impl
{
    sealed class FullCloseGroupEntry : StdCloseGroupEntry, IFullLogEntry
    {
        readonly string _grandOutputId;
        readonly DateTimeStamp _previousLogTime;
        readonly LogEntryType _previousEntryType;

        public FullCloseGroupEntry( string grandOutputId,
                               string monitorId,
                               int depth,
                               DateTimeStamp previousLogTime,
                               LogEntryType previousEntryType,
                               DateTimeStamp t,
                               LogLevel level,
                               IReadOnlyList<ActivityLogGroupConclusion>? c )
            : base( monitorId, depth, t, level, c )
        {
            _grandOutputId = grandOutputId;
            _previousEntryType = previousEntryType;
            _previousLogTime = previousLogTime;
        }

        public string GrandOutputId => _grandOutputId;

        public DateTimeStamp PreviousLogTime => _previousLogTime; 

        public LogEntryType PreviousEntryType => _previousEntryType;

        public ILogEntry CreateSimpleLogEntry() => new StdCloseGroupEntry( this );

        public override void WriteLogEntry( CKBinaryWriter w )
        {
            LogEntry.WriteCloseGroup( w, _grandOutputId, MonitorId, _previousEntryType, _previousLogTime, GroupDepth, LogLevel, LogTime, Conclusions );
        }
    }
}
