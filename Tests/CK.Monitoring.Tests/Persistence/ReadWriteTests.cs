using System;
using System.IO;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using System.Diagnostics;

namespace CK.Monitoring.Tests.Persistence;

[TestFixture]
public class ReadWriteTests
{
    [Test]
    public void LogEntry_write_and_read_back()
    {
        var exInner = new CKExceptionData( "message", "typeof(exception)", "assemblyQualifiedName", "stackTrace", null, "fileName", "fusionLog", null, null );
        var ex2 = new CKExceptionData( "message2", "typeof(exception2)", "assemblyQualifiedName2", "stackTrace2", exInner, "fileName2", "fusionLog2", null, null );
        var exL = new CKExceptionData( "loader-message", "typeof(loader-exception)", "loader-assemblyQualifiedName", "loader-stackTrace", null, "loader-fileName", "loader-fusionLog", null, null );
        var exAgg = new CKExceptionData( "agg-message", "typeof(agg-exception)", "agg-assemblyQualifiedName", "agg-stackTrace", ex2, "fileName", "fusionLog", null, new[] { ex2, exL } );

        var prevLog = DateTimeStamp.UtcNow;
        IBaseLogEntry e1 = LogEntry.CreateLog( "Text1", new DateTimeStamp( DateTime.UtcNow, 42 ), LogLevel.Info, "c:\\test.cs", 3712, ActivityMonitor.Tags.CreateToken, exAgg );
        IBaseLogEntry e2 = LogEntry.CreateMulticastLog( "GOId", "3712", LogEntryType.Line, prevLog, 5, "Text2", DateTimeStamp.UtcNow, LogLevel.Fatal, null, 3712, ActivityMonitor.Tags.CreateToken, exAgg ); ;

        Debug.Assert( e1.Exception != null && e2.Exception != null );

        using( var mem = new MemoryStream() )
        using( var w = new CKBinaryWriter( mem ) )
        {
            w.Write( LogReader.CurrentStreamVersion );
            e1.WriteLogEntry( w );
            e2.WriteLogEntry( w );
            w.Write( (byte)0 );
            w.Flush();

            byte[] versionBytes = new byte[4];
            mem.Position = 0;
            mem.Read( versionBytes, 0, 4 );
            BitConverter.ToInt32( versionBytes, 0 ).ShouldBe( LogReader.CurrentStreamVersion );

            using( var reader = new LogReader( mem, LogReader.CurrentStreamVersion, 4 ) )
            {
                reader.MoveNext().ShouldBeTrue();
                reader.Current.Text.ShouldBe( e1.Text );
                reader.Current.LogLevel.ShouldBe( e1.LogLevel );
                reader.Current.LogTime.ShouldBe( e1.LogTime );
                reader.Current.FileName.ShouldBe( e1.FileName );
                reader.Current.LineNumber.ShouldBe( e1.LineNumber );
                Debug.Assert( reader.Current.Exception != null );
                reader.Current.Exception.ExceptionTypeAssemblyQualifiedName.ShouldBe( e1.Exception.ExceptionTypeAssemblyQualifiedName );
                reader.Current.Exception.ToString().ShouldBe( e1.Exception.ToString() );

                reader.MoveNext().ShouldBeTrue();
                Debug.Assert( reader.CurrentMulticast != null );
                reader.CurrentMulticast.GrandOutputId.ShouldBe( "GOId" );
                reader.CurrentMulticast.MonitorId.ShouldBe( "3712" );
                reader.CurrentMulticast.PreviousEntryType.ShouldBe( LogEntryType.Line );
                reader.CurrentMulticast.PreviousLogTime.ShouldBe( prevLog );
                reader.Current.Text.ShouldBe( e2.Text );
                reader.Current.LogTime.ShouldBe( e2.LogTime );
                reader.Current.FileName.ShouldBeNull();
                reader.Current.LineNumber.ShouldBe( 0, "Since no file name is set, line number is 0." );
                reader.Current.Exception.ExceptionTypeAssemblyQualifiedName.ShouldBe( e2.Exception.ExceptionTypeAssemblyQualifiedName );
                reader.Current.Exception.ToString().ShouldBe( e2.Exception.ToString() );

                reader.MoveNext().ShouldBeFalse();
                reader.BadEndOfFileMarker.ShouldBeFalse();
            }
        }

    }

}
