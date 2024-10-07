using CK.Core;
using System.Diagnostics;

namespace CK.Monitoring.Impl;

class StdOpenGroupEntry : BaseOpenGroupEntry, ILogEntry
{
    readonly string _monitorId;
    readonly int _depth;

    public StdOpenGroupEntry( string monitorId,
                                  int depth,
                                  string text,
                                  DateTimeStamp t,
                                  string? fileName,
                                  int lineNumber,
                                  LogLevel l,
                                  CKTrait tags,
                                  CKExceptionData? ex )
        : base( text, t, fileName, lineNumber, l, tags, ex )
    {
        _monitorId = monitorId;
        _depth = depth;
    }

    public StdOpenGroupEntry( StdOpenGroupEntry e )
        : base( e )
    {
        _monitorId = e._monitorId;
        _depth = e._depth;
    }

    public string MonitorId => _monitorId;

    public int GroupDepth => _depth;

    public override void WriteLogEntry( CKBinaryWriter w )
    {
        Debug.Assert( Text != null, "Only CloseGroup has a null Text." );
        LogEntry.WriteLog( w,
                           _monitorId,
                           _depth,
                           true,
                           LogLevel,
                           LogTime,
                           Text,
                           Tags,
                           Exception,
                           FileName,
                           LineNumber );
    }

    public IBaseLogEntry CreateLightLogEntry() => new BaseOpenGroupEntry( this );

}
