using CK.Core;
using System;
using System.Diagnostics;

namespace CK.Monitoring.Impl
{
    sealed class FullOpenGroupEntry : StdOpenGroupEntry, IFullLogEntry
    {
        readonly string _grandOutputId;
        readonly DateTimeStamp _previousLogTime;
        readonly LogEntryType _previousEntryType;

        public FullOpenGroupEntry( string grandOutputId,
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

        public LogEntryType PreviousEntryType => _previousEntryType;

        public DateTimeStamp PreviousLogTime => _previousLogTime;

        public ILogEntry CreateSimpleLogEntry() => new StdOpenGroupEntry( this );

        public override void WriteLogEntry( CKBinaryWriter w )
        {
            Debug.Assert( Text != null, "Only CloseGroup has a null Text." );
            LogEntry.WriteLog( w,
                               _grandOutputId,
                               MonitorId,
                               _previousEntryType,
                               _previousLogTime,
                               GroupDepth,
                               true,
                               LogLevel,
                               LogTime,
                               Text,
                               Tags,
                               Exception,
                               FileName,
                               LineNumber );
        }

    }
}
