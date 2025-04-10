using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CK.Core;
using CK.Monitoring.Impl;

namespace CK.Monitoring;

/// <summary>
/// Encapsulates <see cref="IBaseLogEntry"/> concrete objects manipulation.
/// </summary>
public static class LogEntry
{
    #region Unicast

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for a line.
    /// </summary>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <returns>A log entry object.</returns>
    public static IBaseLogEntry CreateLog( string text, DateTimeStamp t, LogLevel level, string fileName, int lineNumber, CKTrait tags, CKExceptionData ex )
    {
        return new BaseLineEntry( text, t, fileName, lineNumber, level, tags, ex );
    }

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for an opened group.
    /// </summary>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <returns>A log entry object.</returns>
    public static IBaseLogEntry CreateOpenGroup( string text, DateTimeStamp t, LogLevel level, string fileName, int lineNumber, CKTrait tags, CKExceptionData ex )
    {
        return new BaseOpenGroupEntry( text, t, fileName, lineNumber, level, tags, ex );
    }

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for the closing of a group.
    /// </summary>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="c">Group conclusions.</param>
    /// <returns>A log entry object.</returns>
    public static IBaseLogEntry CreateCloseGroup( DateTimeStamp t, LogLevel level, IReadOnlyList<ActivityLogGroupConclusion> c )
    {
        return new BaseCloseGroupEntry( t, level, c );
    }

    #endregion

    #region Multi-cast

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for a line.
    /// </summary>
    /// <param name="grandOutputId">Identifier of the GrandOutput.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="previousEntryType">Log type of the previous entry in the monitor..</param>
    /// <param name="previousLogTime">Time stamp of the previous entry in the monitor.</param>
    /// <param name="depth">Depth of the line (number of opened groups above).</param>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <returns>A log entry object.</returns>
    public static IFullLogEntry CreateMulticastLog( string grandOutputId,
                                                         string monitorId,
                                                         LogEntryType previousEntryType,
                                                         DateTimeStamp previousLogTime,
                                                         int depth,
                                                         string text,
                                                         DateTimeStamp t,
                                                         LogLevel level,
                                                         string? fileName,
                                                         int lineNumber,
                                                         CKTrait tags,
                                                         CKExceptionData? ex )
    {
        return new FullLineEntry( grandOutputId, monitorId, depth, previousLogTime, previousEntryType, text, t, fileName, lineNumber, level, tags, ex );
    }

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for an opened group.
    /// </summary>
    /// <param name="grandOutputId">Identifier of the GrandOutput.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="previousEntryType">Log type of the previous entry in the monitor..</param>
    /// <param name="previousLogTime">Time stamp of the previous entry in the monitor.</param>
    /// <param name="depth">Depth of the line (number of opened groups above).</param>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <returns>A log entry object.</returns>
    public static IFullLogEntry CreateMulticastOpenGroup( string grandOutputId,
                                                               string monitorId,
                                                               LogEntryType previousEntryType,
                                                               DateTimeStamp previousLogTime,
                                                               int depth,
                                                               string text,
                                                               DateTimeStamp t,
                                                               LogLevel level,
                                                               string? fileName,
                                                               int lineNumber,
                                                               CKTrait tags,
                                                               CKExceptionData? ex )
    {
        return new FullOpenGroupEntry( grandOutputId, monitorId, depth, previousLogTime, previousEntryType, text, t, fileName, lineNumber, level, tags, ex );
    }

    /// <summary>
    /// Creates a <see cref="IBaseLogEntry"/> for the closing of a group.
    /// </summary>
    /// <param name="grandOutputId">Identifier of the GrandOutput.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="previousEntryType">Log type of the previous entry in the monitor..</param>
    /// <param name="previousLogTime">Time stamp of the previous entry in the monitor.</param>
    /// <param name="depth">Depth of the line (number of opened groups above).</param>
    /// <param name="t">Time stamp of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="c">Group conclusions.</param>
    /// <returns>A log entry object.</returns>
    public static IFullLogEntry CreateMulticastCloseGroup( string grandOutputId,
                                                                string monitorId,
                                                                LogEntryType previousEntryType,
                                                                DateTimeStamp previousLogTime,
                                                                int depth,
                                                                DateTimeStamp t,
                                                                LogLevel level,
                                                                IReadOnlyList<ActivityLogGroupConclusion>? c )
    {
        return new FullCloseGroupEntry( grandOutputId, monitorId, depth, previousLogTime, previousEntryType, t, level, c );
    }

    #endregion

    /// <summary>
    /// Binary writes a <see cref="ILogEntry"/> Line or OpenGroup entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="depth">Depth of the line (number of opened groups above).</param>
    /// <param name="isOpenGroup">True if this the opening of a group. False for a line.</param>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="logTime">Time stamp of the log entry.</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    static public void WriteLog( CKBinaryWriter w,
                                 string monitorId,
                                 int depth,
                                 bool isOpenGroup,
                                 LogLevel level,
                                 DateTimeStamp logTime,
                                 string text,
                                 CKTrait tags,
                                 CKExceptionData? ex,
                                 string? fileName,
                                 int lineNumber )
    {
        Throw.CheckNotNullArgument( w );
        StreamLogType type = StreamLogType.IsSimpleLogEntry | (isOpenGroup ? StreamLogType.TypeOpenGroup : StreamLogType.TypeLine);
        DoWriteLog( w, type, level, logTime, text, tags, ex, fileName, lineNumber );
        WriteMonitorEntryFooter( w, monitorId, depth );
    }

    /// <summary>
    /// Binary writes a <see cref="IFullLogEntry"/> line or OpenGroup entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="grandOutputId">Identifier of the GrandOutput.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="previousEntryType">Log type of the previous entry in the monitor..</param>
    /// <param name="previousLogTime">Time stamp of the previous entry in the monitor.</param>
    /// <param name="depth">Depth of the line (number of opened groups above).</param>
    /// <param name="isOpenGroup">True if this the opening of a group. False for a line.</param>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="logTime">Time stamp of the log entry.</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    static public void WriteLog( CKBinaryWriter w,
                                 string grandOutputId,
                                 string monitorId,
                                 LogEntryType previousEntryType,
                                 DateTimeStamp previousLogTime,
                                 int depth,
                                 bool isOpenGroup,
                                 LogLevel level,
                                 DateTimeStamp logTime,
                                 string text,
                                 CKTrait tags,
                                 CKExceptionData? ex,
                                 string? fileName,
                                 int lineNumber )
    {
        Throw.CheckNotNullArgument( w );
        StreamLogType type = StreamLogType.IsFullEntry | (isOpenGroup ? StreamLogType.TypeOpenGroup : StreamLogType.TypeLine);
        type = UpdateTypeWithPrevious( type, previousEntryType, ref previousLogTime );
        DoWriteLog( w, type, level, logTime, text, tags, ex, fileName, lineNumber );
        WriteMulticastFooter( w, grandOutputId, monitorId, previousEntryType, previousLogTime, depth );
    }

    /// <summary>
    /// Binary writes a unicast log entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="isOpenGroup">True if this the opening of a group. False for a line.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="text">Text of the log entry.</param>
    /// <param name="logTime">Time stamp of the log entry.</param>
    /// <param name="tags">Tags of the log entry</param>
    /// <param name="ex">Exception of the log entry.</param>
    /// <param name="fileName">Source file name of the log entry</param>
    /// <param name="lineNumber">Source line number of the log entry</param>
    static public void WriteLog( CKBinaryWriter w,
                                 bool isOpenGroup,
                                 LogLevel level,
                                 DateTimeStamp logTime,
                                 string text,
                                 CKTrait tags,
                                 CKExceptionData? ex,
                                 string? fileName,
                                 int lineNumber )
    {
        if( w == null ) throw new ArgumentNullException( "w" );
        DoWriteLog( w, isOpenGroup ? StreamLogType.TypeOpenGroup : StreamLogType.TypeLine, level, logTime, text, tags, ex, fileName, lineNumber );
    }

    static void DoWriteLog( CKBinaryWriter w, StreamLogType t, LogLevel level, DateTimeStamp logTime, string text, CKTrait tags, CKExceptionData? ex, string? fileName, int lineNumber )
    {
        if( tags != null && !tags.IsEmpty ) t |= StreamLogType.HasTags;
        if( ex != null )
        {
            t |= StreamLogType.HasException;
            if( text == ex.Message ) t |= StreamLogType.IsTextTheExceptionMessage;
        }
        if( fileName != null ) t |= StreamLogType.HasFileName;
        if( logTime.Uniquifier != 0 ) t |= StreamLogType.HasUniquifier;

        WriteLogTypeAndLevel( w, t, level );
        w.Write( logTime.TimeUtc.ToBinary() );
        if( logTime.Uniquifier != 0 ) w.Write( logTime.Uniquifier );
        if( (t & StreamLogType.HasTags) != 0 ) w.Write( tags!.ToString() );
        if( (t & StreamLogType.HasFileName) != 0 )
        {
            Debug.Assert( fileName != null );
            w.Write( fileName );
            w.WriteNonNegativeSmallInt32( lineNumber );
        }
        if( (t & StreamLogType.HasException) != 0 ) ex!.Write( w );
        if( (t & StreamLogType.IsTextTheExceptionMessage) == 0 ) w.Write( text );
    }

    /// <summary>
    /// Binary writes a closing <see cref="IBaseLogEntry"/> entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="closeTime">Time stamp of the group closing.</param>
    /// <param name="conclusions">Group conclusions.</param>
    static public void WriteCloseGroup( CKBinaryWriter w,
                                        LogLevel level,
                                        DateTimeStamp closeTime,
                                        IReadOnlyList<ActivityLogGroupConclusion>? conclusions )
    {
        Throw.CheckNotNullArgument( w );
        DoWriteCloseGroup( w, StreamLogType.TypeGroupClosed, level, closeTime, conclusions );
    }

    /// <summary>
    /// Binary writes a <see cref="ILogEntry"/> closing entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="depth">Depth of the group (number of opened groups above).</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="closeTime">Time stamp of the group closing.</param>
    /// <param name="conclusions">Group conclusions.</param>
    static public void WriteCloseGroup( CKBinaryWriter w,
                                        string monitorId,
                                        int depth,
                                        LogLevel level,
                                        DateTimeStamp closeTime,
                                        IReadOnlyList<ActivityLogGroupConclusion>? conclusions )
    {
        Throw.CheckNotNullArgument( w );
        StreamLogType type = StreamLogType.TypeGroupClosed | StreamLogType.IsSimpleLogEntry;
        DoWriteCloseGroup( w, type, level, closeTime, conclusions );
        WriteMonitorEntryFooter( w, monitorId, depth );
    }


    /// <summary>
    /// Binary writes a <see cref="IFullLogEntry"/> closing entry.
    /// </summary>
    /// <param name="w">Binary writer to use.</param>
    /// <param name="grandOutputId">Identifier of the GrandOutput.</param>
    /// <param name="monitorId">Identifier of the monitor.</param>
    /// <param name="previousEntryType">Log type of the previous entry in the monitor..</param>
    /// <param name="previousLogTime">Time stamp of the previous entry in the monitor.</param>
    /// <param name="depth">Depth of the group (number of opened groups above).</param>
    /// <param name="level">Log level of the log entry.</param>
    /// <param name="closeTime">Time stamp of the group closing.</param>
    /// <param name="conclusions">Group conclusions.</param>
    static public void WriteCloseGroup( CKBinaryWriter w,
                                        string grandOutputId,
                                        string monitorId,
                                        LogEntryType previousEntryType,
                                        DateTimeStamp previousLogTime,
                                        int depth,
                                        LogLevel level,
                                        DateTimeStamp closeTime,
                                        IReadOnlyList<ActivityLogGroupConclusion>? conclusions )
    {
        Throw.CheckNotNullArgument( w );
        StreamLogType type = StreamLogType.TypeGroupClosed | StreamLogType.IsFullEntry;
        type = UpdateTypeWithPrevious( type, previousEntryType, ref previousLogTime );
        DoWriteCloseGroup( w, type, level, closeTime, conclusions );
        WriteMulticastFooter( w, grandOutputId, monitorId, previousEntryType, previousLogTime, depth );
    }

    static StreamLogType UpdateTypeWithPrevious( StreamLogType type, LogEntryType previousEntryType, ref DateTimeStamp previousStamp )
    {
        if( previousStamp.IsKnown )
        {
            type |= StreamLogType.IsPreviousKnown;
            if( previousEntryType == LogEntryType.None ) throw new ArgumentException( "Must not be None since previousStamp is known.", "previousEntryType" );
            if( previousStamp.Uniquifier != 0 ) type |= StreamLogType.IsPreviousKnownHasUniquifier;
        }
        return type;
    }

    static void WriteMulticastFooter( CKBinaryWriter w, string grandOutputId, string monitorId, LogEntryType previousEntryType, DateTimeStamp previousStamp, int depth )
    {
        w.Write( grandOutputId );
        w.Write( monitorId );
        w.WriteNonNegativeSmallInt32( depth );
        if( previousStamp.IsKnown )
        {
            w.Write( previousStamp.TimeUtc.ToBinary() );
            if( previousStamp.Uniquifier != 0 ) w.Write( previousStamp.Uniquifier );
            w.Write( (byte)previousEntryType );
        }
    }

    static void WriteMonitorEntryFooter( CKBinaryWriter w, string monitorId, int depth )
    {
        w.Write( monitorId );
        w.WriteNonNegativeSmallInt32( depth );
    }

    static void DoWriteCloseGroup( CKBinaryWriter w, StreamLogType t, LogLevel level, DateTimeStamp closeTime, IReadOnlyList<ActivityLogGroupConclusion>? conclusions )
    {
        if( conclusions != null && conclusions.Count > 0 ) t |= StreamLogType.HasConclusions;
        if( closeTime.Uniquifier != 0 ) t |= StreamLogType.HasUniquifier;
        WriteLogTypeAndLevel( w, t, level );
        w.Write( closeTime.TimeUtc.ToBinary() );
        if( closeTime.Uniquifier != 0 ) w.Write( closeTime.Uniquifier );
        if( (t & StreamLogType.HasConclusions) != 0 )
        {
            Debug.Assert( conclusions != null );
            w.WriteNonNegativeSmallInt32( conclusions.Count );
            foreach( ActivityLogGroupConclusion c in conclusions )
            {
                w.Write( c.Tag.ToString() );
                w.Write( c.Text );
            }
        }
    }

    /// <summary>
    /// Reads a <see cref="IBaseLogEntry"/> from the binary reader that can be a <see cref="IFullLogEntry"/>.
    /// If the first read byte is 0, read stops and null is returned.
    /// The 0 byte is the "end marker" that <see cref="CKMonWriterClient.Close()"/> write, but this
    /// method can read non zero-terminated streams (it catches an EndOfStreamException when reading the first byte and handles it silently).
    /// This method can throw any type of exception except <see cref="EndOfStreamException"/>
    /// (like <see cref="InvalidDataException"/> for instance) that must be handled by the caller.
    /// </summary>
    /// <param name="r">The binary reader.</param>
    /// <param name="streamVersion">The version of the stream.</param>
    /// <param name="badEndOfFile">True whenever the end of file is the result of an <see cref="EndOfStreamException"/>.</param>
    /// <returns>The log entry or null if a zero byte (end marker) has been found.</returns>
    static public IBaseLogEntry? Read( CKBinaryReader r, int streamVersion, out bool badEndOfFile )
    {
        Throw.CheckNotNullArgument( r );
        badEndOfFile = false;
        StreamLogType t = StreamLogType.EndOfStream;
        LogLevel logLevel = LogLevel.None;
        try
        {
            ReadLogTypeAndLevel( r, streamVersion, out t, out logLevel );
        }
        catch( EndOfStreamException )
        {
            badEndOfFile = true;
            // Silently ignores here reading beyond the stream: this
            // kindly handles the lack of terminating 0 byte.
        }
        if( t == StreamLogType.EndOfStream ) return null;

        if( (t & StreamLogType.TypeMask) == StreamLogType.TypeGroupClosed )
        {
            return ReadGroupClosed( streamVersion, r, t, logLevel );
        }
        DateTimeStamp time = new DateTimeStamp( DateTime.FromBinary( r.ReadInt64() ), (t & StreamLogType.HasUniquifier) != 0 ? r.ReadByte() : (Byte)0 );
        Throw.CheckData( "Year before 2014 or after 3000 is considered invalid.", time.TimeUtc.Year >= 2014 && time.TimeUtc.Year < 3000 );
        CKTrait tags = ActivityMonitor.Tags.Empty;
        string? fileName = null;
        int lineNumber = 0;
        CKExceptionData? ex = null;
        string? text = null;

        if( (t & StreamLogType.HasTags) != 0 ) tags = ActivityMonitor.Tags.Register( r.ReadString() );
        if( (t & StreamLogType.HasFileName) != 0 )
        {
            fileName = r.ReadString();
            lineNumber = streamVersion < 6 ? r.ReadInt32() : r.ReadNonNegativeSmallInt32();
            Throw.CheckData( lineNumber <= 100 * 1000 );
        }
        if( (t & StreamLogType.HasException) != 0 )
        {
            ex = new CKExceptionData( r );
            if( (t & StreamLogType.IsTextTheExceptionMessage) != 0 ) text = ex.Message;
        }
        if( text == null ) text = r.ReadString( (t & StreamLogType.IsLFOnly) == 0 );

        string gId;
        string mId;
        int depth;
        LogEntryType prevType;
        DateTimeStamp prevTime;

        if( (t & StreamLogType.TypeMask) == StreamLogType.TypeLine )
        {
            if( (t & StreamLogType.IsFullEntry) == 0 )
            {
                return new BaseLineEntry( text, time, fileName, lineNumber, logLevel, tags, ex );
            }
            ReadMulticastFooter( streamVersion, r, t, out gId, out mId, out depth, out prevType, out prevTime );
            return new FullLineEntry( gId, mId, depth, prevTime, prevType, text, time, fileName, lineNumber, logLevel, tags, ex );
        }
        if( (t & StreamLogType.TypeMask) != StreamLogType.TypeOpenGroup ) throw new InvalidDataException();
        if( (t & StreamLogType.IsFullEntry) == 0 )
        {
            return new BaseOpenGroupEntry( text, time, fileName, lineNumber, logLevel, tags, ex );
        }
        ReadMulticastFooter( streamVersion, r, t, out gId, out mId, out depth, out prevType, out prevTime );
        return new FullOpenGroupEntry( gId, mId, depth, prevTime, prevType, text, time, fileName, lineNumber, logLevel, tags, ex );
    }

    static void ReadMonitorEntryFooter( int streamVersion, CKBinaryReader r, StreamLogType t, out string mId, out int depth )
    {
        mId = r.ReadString();
        Throw.CheckData( mId == ActivityMonitor.ExternalLogMonitorUniqueId || mId == ActivityMonitor.StaticLogMonitorUniqueId || Base64UrlHelper.IsBase64UrlCharacters( mId ) );
        depth = r.ReadNonNegativeSmallInt32();
        Throw.CheckData( depth >= 0 );
    }

    static void ReadMulticastFooter( int streamVersion, CKBinaryReader r, StreamLogType t, out string gId, out string mId, out int depth, out LogEntryType prevType, out DateTimeStamp prevTime )
    {
        if( streamVersion >= 9 )
        {
            gId = r.ReadString();
            mId = r.ReadString();
            depth = r.ReadNonNegativeSmallInt32();
            Throw.CheckData( mId == ActivityMonitor.ExternalLogMonitorUniqueId || mId == ActivityMonitor.StaticLogMonitorUniqueId || Base64UrlHelper.IsBase64UrlCharacters( mId ) );
        }
        else
        {
            gId = GrandOutput.UnknownGrandOutputId;
            Debug.Assert( Guid.Empty.ToByteArray().Length == 16 );
            mId = streamVersion < 8 ? new Guid( r.ReadBytes( 16 ) ).ToString() : r.ReadString();
            depth = streamVersion < 6 ? r.ReadInt32() : r.ReadNonNegativeSmallInt32();
            if( streamVersion >= 8 )
            {
                Throw.CheckData( mId == ActivityMonitor.ExternalLogMonitorUniqueId || Base64UrlHelper.IsBase64UrlCharacters( mId ) );
            }
        }
        Throw.CheckData( gId == GrandOutput.UnknownGrandOutputId || Base64UrlHelper.IsBase64UrlCharacters( gId ) );
        Throw.CheckData( depth >= 0 );
        prevType = LogEntryType.None;
        prevTime = DateTimeStamp.Unknown;
        if( (t & StreamLogType.IsPreviousKnown) != 0 )
        {
            prevTime = new DateTimeStamp( DateTime.FromBinary( r.ReadInt64() ), (t & StreamLogType.IsPreviousKnownHasUniquifier) != 0 ? r.ReadByte() : (Byte)0 );
            prevType = (LogEntryType)r.ReadByte();
        }
    }

    static IBaseLogEntry ReadGroupClosed( int streamVersion, CKBinaryReader r, StreamLogType t, LogLevel logLevel )
    {
        DateTimeStamp time = new DateTimeStamp( DateTime.FromBinary( r.ReadInt64() ), (t & StreamLogType.HasUniquifier) != 0 ? r.ReadByte() : (Byte)0 );
        ActivityLogGroupConclusion[] conclusions = Array.Empty<ActivityLogGroupConclusion>();
        if( (t & StreamLogType.HasConclusions) != 0 )
        {
            int conclusionsCount = streamVersion < 6 ? r.ReadInt32() : r.ReadNonNegativeSmallInt32();
            conclusions = new ActivityLogGroupConclusion[conclusionsCount];
            for( int i = 0; i < conclusionsCount; i++ )
            {
                CKTrait cTags = ActivityMonitor.Tags.Register( r.ReadString() );
                string cText = r.ReadString();
                conclusions[i] = new ActivityLogGroupConclusion( cText, cTags );
            }
        }
        if( (t & (StreamLogType.IsFullEntry | StreamLogType.IsSimpleLogEntry)) == 0 )
        {
            return new BaseCloseGroupEntry( time, logLevel, conclusions );
        }
        string mId;
        int depth;
        if( (t & StreamLogType.IsSimpleLogEntry) != 0 )
        {
            ReadMonitorEntryFooter( streamVersion, r, t, out mId, out depth );
            return new StdCloseGroupEntry( mId, depth, time, logLevel, conclusions );
        }
        ReadMulticastFooter( streamVersion, r, t, out var gId, out mId, out depth, out LogEntryType prevType, out DateTimeStamp prevTime );
        return new FullCloseGroupEntry( gId, mId, depth, prevTime, prevType, time, logLevel, conclusions );
    }

    static void WriteLogTypeAndLevel( BinaryWriter w, StreamLogType t, LogLevel level )
    {
        Debug.Assert( (int)StreamLogType.MaxFlag < (1 << 16) );
        Debug.Assert( (int)LogLevel.NumberOfBits < 8 );
        w.Write( (byte)level );
        if( !StringAndStringBuilderExtension.IsCRLF ) t |= StreamLogType.IsLFOnly;
        w.Write( (ushort)t );
    }

    static void ReadLogTypeAndLevel( BinaryReader r, int streamVersion, out StreamLogType t, out LogLevel l )
    {
        Debug.Assert( (int)StreamLogType.MaxFlag < (1 << 16) );
        Debug.Assert( (int)LogLevel.NumberOfBits < 8 );

        t = StreamLogType.EndOfStream;
        l = LogLevel.Trace;

        byte level = r.ReadByte();
        // Found the 0 end marker?
        if( level != 0 )
        {
            Throw.CheckData( level < (1 << (int)LogLevel.NumberOfBits) );
            if( streamVersion < 7 ) level <<= 1;
            l = (LogLevel)level;

            ushort type = r.ReadUInt16();
            Throw.CheckData( type < ((int)StreamLogType.MaxFlag * 2 - 1) );
            t = (StreamLogType)type;
        }
    }

    static readonly string _missingLineText = "<Missing log data>";
    static readonly string _missingGroupText = "<Missing group>";
    static readonly IReadOnlyList<ActivityLogGroupConclusion> _missingConclusions = Array.Empty<ActivityLogGroupConclusion>();

    static internal IBaseLogEntry CreateMissingLine( DateTimeStamp knownTime )
    {
        Debug.Assert( !knownTime.IsInvalid );
        return new BaseLineEntry( _missingLineText, knownTime, null, 0, LogLevel.None, ActivityMonitor.Tags.Empty, null );
    }

    static internal IBaseLogEntry CreateMissingOpenGroup( DateTimeStamp knownTime )
    {
        Debug.Assert( !knownTime.IsInvalid );
        return new BaseOpenGroupEntry( _missingGroupText, knownTime, null, 0, LogLevel.None, ActivityMonitor.Tags.Empty, null );
    }

    static internal IBaseLogEntry CreateMissingCloseGroup( DateTimeStamp knownTime )
    {
        Debug.Assert( !knownTime.IsInvalid );
        return new BaseCloseGroupEntry( knownTime, LogLevel.None, _missingConclusions );
    }

    internal static bool IsMissingLogEntry( IBaseLogEntry entry )
    {
        Debug.Assert( entry != null );
        return ReferenceEquals( entry.Text, _missingGroupText ) || ReferenceEquals( entry.Text, _missingLineText ) || entry.Conclusions == _missingConclusions;
    }
}
