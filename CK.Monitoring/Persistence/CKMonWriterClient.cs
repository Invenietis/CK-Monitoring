using System.Collections.Generic;
using System.Diagnostics;
using CK.Core;
using CK.Core.Impl;

namespace CK.Monitoring;

/// <summary>
/// This client writes .ckmon files for one monitor.
/// To close output file, simply <see cref="IActivityMonitorOutput.UnregisterClient">unregister</see> this client.
/// </summary>
public sealed class CKMonWriterClient : IActivityMonitorBoundClient, IFullLogInfo
{
    readonly string _path;
    readonly int _maxCountPerFile;
    readonly LogFilter _minimalFilter;
    readonly string _grandOutputId;
    IActivityMonitorImpl? _source;
    MonitorBinaryFileOutput? _file;
    int _entryDepth;
    DateTimeStamp _prevlogTime;
    LogEntryType _prevLogType;
    readonly bool _useGzipCompression;

    /// <summary>
    /// Initializes a new instance of <see cref="CKMonWriterClient"/> that can be registered to write uncompressed .ckmon file for this monitor.
    /// </summary>
    /// <param name="path">The path. Can be absolute. When relative, it will be under <see cref="LogFile.RootLogPath"/> that must be set.</param>
    /// <param name="maxCountPerFile">Maximum number of entries per file. Must be greater than 1.</param>
    /// <param name="grandOutputId">
    /// Optional <see cref="GrandOutput.GrandOutpuId"/>. Defaults to the one of the current <see cref="GrandOutput.Default"/>
    /// if it is not null otherwise falls back to <see cref="GrandOutput.UnknownGrandOutputId"/>.
    /// </param>
    public CKMonWriterClient( string path, int maxCountPerFile, string? grandOutputId = null )
        : this( path, maxCountPerFile, LogFilter.Undefined, false, grandOutputId )
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CKMonWriterClient"/> that can be registered to write compressed or uncompressed .ckmon file for this monitor.
    /// </summary>
    /// <param name="path">The path. Can be absolute. When relative, it will be under <see cref="LogFile.RootLogPath"/> that must be set.</param>
    /// <param name="maxCountPerFile">Maximum number of entries per file. Must be greater than 1.</param>
    /// <param name="minimalFilter">Minimal filter for this client.</param>
    /// <param name="useGzipCompression">Whether to output compressed .ckmon files. Defaults to false (do not compress).</param>
    /// <param name="grandOutputId">
    /// Optional <see cref="GrandOutput.GrandOutpuId"/>. Defaults to the one of the current <see cref="GrandOutput.Default"/>
    /// if it is not null otherwise falls back to <see cref="GrandOutput.UnknownGrandOutputId"/>.
    /// </param>
    public CKMonWriterClient( string path, int maxCountPerFile, LogFilter minimalFilter, bool useGzipCompression = false, string? grandOutputId = null )
    {
        _path = path;
        _maxCountPerFile = maxCountPerFile;
        _minimalFilter = minimalFilter;
        _useGzipCompression = useGzipCompression;
        _grandOutputId = grandOutputId ?? GrandOutput.Default?.GrandOutpuId ?? GrandOutput.UnknownGrandOutputId;
    }

    /// <summary>
    /// Gets the minimal filter set by the constructor.
    /// </summary>
    public LogFilter MinimalFilter => _minimalFilter;

    bool IActivityMonitorBoundClient.IsDead => false;

    void IActivityMonitorBoundClient.SetMonitor( IActivityMonitorImpl? source, bool forceBuggyRemove )
    {
        if( source != null && _source != null ) ActivityMonitorClient.ThrowMultipleRegisterOnBoundClientException( this );
        // Silently ignore null => null or monitor => same monitor.
        if( source != _source )
        {
            _prevLogType = LogEntryType.None;
            _prevlogTime = DateTimeStamp.Unknown;
            Debug.Assert( (source == null) != (_source == null) );
            if( (_source = source) == null )
            {
                if( _file != null ) _file.Close();
                _file = null;
            }
            else
            {
                // If initialization failed, we let the file null: this monitor will not
                // work (the error will appear in the Critical errors) but this avoids
                // an exception to be thrown here.
                var f = new MonitorBinaryFileOutput( _path, _source.InternalMonitor.UniqueId, _maxCountPerFile, _useGzipCompression );
                if( f.Initialize( _source.InternalMonitor ) )
                {
                    _file = f;
                }
            }
        }
    }

    /// <summary>
    /// Opens this writer if it is not already opened.
    /// </summary>
    /// <returns>True on success, false otherwise.</returns>
    public bool Open()
    {
        Throw.CheckState( "CKMonWriterClient must be registered in an ActivityMonitor.", _source != null );
        if( _file != null ) return true;
        _file = new MonitorBinaryFileOutput( _path, _source.InternalMonitor.UniqueId, _maxCountPerFile, _useGzipCompression );
        _prevLogType = LogEntryType.None;
        _prevlogTime = DateTimeStamp.Unknown;
        if( !_file.Initialize( _source.InternalMonitor ) ) _file = null;
        return _file != null;
    }

    /// <summary>
    /// Closes this writer if it <see cref="IsOpened"/>.
    /// It can be re-<see cref="Open"/>ed later.
    /// </summary>
    public void Close()
    {
        if( _file != null ) _file.Close();
        _file = null;
    }

    /// <summary>
    /// Gets whether this writer is opened.
    /// </summary>
    public bool IsOpened => _file != null;

    #region Auto implementation of IFullLogInfo to call Write on file.

    string IFullLogInfo.GrandOutputId => _grandOutputId;

    LogEntryType IFullLogInfo.PreviousEntryType
    {
        get
        {
            Debug.Assert( _source != null && _file != null );
            return _prevLogType;
        }
    }
    DateTimeStamp IFullLogInfo.PreviousLogTime
    {
        get
        {
            Debug.Assert( _source != null && _file != null );
            return _prevlogTime;
        }
    }
    #endregion

    void IActivityMonitorClient.OnUnfilteredLog( ref ActivityMonitorLogData data )
    {
        if( _file != null )
        {
            _file.WriteLineEntry( data, this );
            _prevlogTime = data.LogTime;
            _prevLogType = LogEntryType.Line;
        }
    }
    void IActivityMonitorClient.OnOpenGroup( IActivityLogGroup group )
    {
        if( _file != null )
        {
            _entryDepth = group.Data.Depth;
            _file.WriteOpenGroupEntry( group, this );
            _prevlogTime = group.Data.LogTime;
            _prevLogType = LogEntryType.OpenGroup;
        }
    }
    void IActivityMonitorClient.OnGroupClosed( IActivityLogGroup group, IReadOnlyList<ActivityLogGroupConclusion>? conclusions )
    {
        if( _file != null )
        {
            _entryDepth = group.Data.Depth;
            _file.WriteCloseGroupEntry( group, conclusions, this );
            _prevlogTime = group.CloseLogTime;
            _prevLogType = LogEntryType.CloseGroup;
        }
    }
    void IActivityMonitorClient.OnGroupClosing( IActivityLogGroup group, ref List<ActivityLogGroupConclusion>? conclusions )
    {
    }
    void IActivityMonitorClient.OnTopicChanged( string newTopic, string? fileName, int lineNumber )
    {
        // Does nothing.
    }
    void IActivityMonitorClient.OnAutoTagsChanged( CKTrait newTrait )
    {
        // Does nothing.
    }
}
