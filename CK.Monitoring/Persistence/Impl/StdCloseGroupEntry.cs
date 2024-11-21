using CK.Core;
using System.Collections.Generic;

namespace CK.Monitoring.Impl;

class StdCloseGroupEntry : BaseCloseGroupEntry, ILogEntry
{
    readonly string _monitorId;
    readonly int _depth;

    public StdCloseGroupEntry( string monitorId,
                                   int depth,
                                   DateTimeStamp t,
                                   LogLevel level,
                                   IReadOnlyList<ActivityLogGroupConclusion>? c )
        : base( t, level, c )
    {
        _monitorId = monitorId;
        _depth = depth;
    }

    public StdCloseGroupEntry( StdCloseGroupEntry e )
        : base( e )
    {
        _monitorId = e._monitorId;
        _depth = e._depth;
    }

    public string MonitorId => _monitorId;

    public int GroupDepth => _depth;

    public override void WriteLogEntry( CKBinaryWriter w )
    {
        LogEntry.WriteCloseGroup( w, _monitorId, _depth, LogLevel, LogTime, Conclusions );
    }

    public IBaseLogEntry CreateLightLogEntry()
    {
        return new BaseCloseGroupEntry( this );
    }

}
