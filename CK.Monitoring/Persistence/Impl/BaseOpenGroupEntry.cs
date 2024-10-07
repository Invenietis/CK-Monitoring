using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Monitoring.Impl;

class BaseOpenGroupEntry : IBaseLogEntry
{
    readonly string _text;
    readonly CKTrait _tags;
    readonly string? _fileName;
    readonly int _lineNumber;
    readonly LogLevel _level;
    readonly CKExceptionData? _ex;
    readonly DateTimeStamp _time;

    public BaseOpenGroupEntry( string text, DateTimeStamp t, string? fileName, int lineNumber, LogLevel l, CKTrait tags, CKExceptionData? ex )
    {
        _text = text;
        _time = t;
        _fileName = fileName;
        _lineNumber = lineNumber;
        _level = l;
        _tags = tags;
        _ex = ex;
    }

    public BaseOpenGroupEntry( StdOpenGroupEntry e )
    {
        Debug.Assert( e.Text != null );
        _text = e.Text;
        _time = e.LogTime;
        _fileName = e.FileName;
        _lineNumber = e.LineNumber;
        _level = e.LogLevel;
        _tags = e.Tags;
        _ex = e.Exception;
    }

    public LogEntryType LogType => LogEntryType.OpenGroup;

    public LogLevel LogLevel => _level;

    public string? Text => _text;

    public CKTrait Tags => _tags;

    public DateTimeStamp LogTime => _time;

    public CKExceptionData? Exception => _ex;

    public string? FileName => _fileName;

    public int LineNumber => _lineNumber;

    public IReadOnlyList<ActivityLogGroupConclusion>? Conclusions => null;

    public virtual void WriteLogEntry( CKBinaryWriter w )
    {
        LogEntry.WriteLog( w, true, _level, _time, _text, _tags, _ex, _fileName, _lineNumber );
    }
}
