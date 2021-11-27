using System;
using System.Diagnostics;
using CK.Core;

namespace CK.Monitoring.Impl
{
    class LEMCLog : LELog, IMulticastLogEntry
    {
        readonly string _monitorId;
        readonly int _depth;
        readonly DateTimeStamp _previousLogTime;
        readonly LogEntryType _previousEntryType;

        public LEMCLog( string monitorId, int depth, DateTimeStamp previousLogTime, LogEntryType previousEntryType, string text, DateTimeStamp t, string? fileName, int lineNumber, LogLevel l, CKTrait tags, CKExceptionData? ex )
            : base( text, t, fileName, lineNumber, l, tags, ex )
        {
            _monitorId = monitorId;
            _depth = depth;
            _previousEntryType = previousEntryType;
            _previousLogTime = previousLogTime;
        }

        public string MonitorId => _monitorId;

        public int GroupDepth => _depth;

        public DateTimeStamp PreviousLogTime => _previousLogTime;

        public LogEntryType PreviousEntryType => _previousEntryType; 

        public override void WriteLogEntry( CKBinaryWriter w )
        {
            Debug.Assert( Text != null, "Only LE(MC)CloseGroup has a null Text." );
            LogEntry.WriteLog( w, _monitorId, _previousEntryType, _previousLogTime, _depth, false, LogLevel, LogTime, Text, Tags, Exception, FileName, LineNumber );
        }
        
        public ILogEntry CreateUnicastLogEntry()
        {
            return new LELog( this );
        }
    }
}
