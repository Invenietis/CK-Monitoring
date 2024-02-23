using System;
using System.Diagnostics;
using CK.Core;

namespace CK.Monitoring.Impl
{
    sealed class FullLineEntry : StdLineEntry, IFullLogEntry
    {
        readonly string _grandOutputId;
        readonly DateTimeStamp _previousLogTime;
        readonly LogEntryType _previousEntryType;

        public FullLineEntry( string grandOutputId,
                              string monitorId,
                              int depth,
                              DateTimeStamp previousLogTime,
                              LogEntryType previousEntryType,
                              string text,
                              DateTimeStamp t,
                              string? fileName,
                              int lineNumber,
                              LogLevel l,
                              CKTrait tags,
                              CKExceptionData? ex )
            : base( monitorId, depth, text, t, fileName, lineNumber, l, tags, ex )
        {
            _grandOutputId = grandOutputId;
            _previousEntryType = previousEntryType;
            _previousLogTime = previousLogTime;
        }

        public string GrandOutputId => _grandOutputId;

        public DateTimeStamp PreviousLogTime => _previousLogTime;

        public LogEntryType PreviousEntryType => _previousEntryType;

        public ILogEntry CreateSimpleLogEntry() => new StdLineEntry( this );

        public override void WriteLogEntry( CKBinaryWriter w )
        {
            Debug.Assert( Text != null, "Only LE(MC)CloseGroup has a null Text." );
            LogEntry.WriteLog( w, _grandOutputId, MonitorId, _previousEntryType, _previousLogTime, GroupDepth, false, LogLevel, LogTime, Text, Tags, Exception, FileName, LineNumber );
        }
    }
}
