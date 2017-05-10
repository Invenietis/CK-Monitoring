using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using NUnit.Framework;
using CK.Text;
using FluentAssertions;

namespace CK.Monitoring.Tests.Persistence
{
    [TestFixture]
    public class ReadWriteTests
    {
        [Test]
        public void LogEntry_write_and_read_back()
        {
            var exInner = new CKExceptionData( "message", "typeof(exception)", "assemblyQualifiedName", "stackTrace", null, "fileName", "fusionLog", null, null );
            var ex2 = new CKExceptionData( "message2", "typeof(exception2)", "assemblyQualifiedName2", "stackTrace2", exInner, "fileName2", "fusionLog2", null, null );
            var exL = new CKExceptionData( "loader-message", "typeof(loader-exception)", "loader-assemblyQualifiedName", "loader-stackTrace", null, "loader-fileName", "loader-fusionLog", null, null );
            var exAgg = new CKExceptionData( "agg-message", "typeof(agg-exception)", "agg-assemblyQualifiedName", "agg-stackTrace", ex2, "fileName", "fusionLog", null, new[]{ ex2, exL } );

            var prevLog = DateTimeStamp.UtcNow;
            ILogEntry e1 = LogEntry.CreateLog( "Text1", new DateTimeStamp( DateTime.UtcNow, 42 ), LogLevel.Info, "c:\\test.cs", 3712, ActivityMonitor.Tags.CreateDependentActivity, exAgg );
            ILogEntry e2 = LogEntry.CreateMulticastLog( Guid.Empty, LogEntryType.Line, prevLog, 5, "Text2", DateTimeStamp.UtcNow, LogLevel.Fatal, null, 3712, ActivityMonitor.Tags.CreateDependentActivity, exAgg );

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
                BitConverter.ToInt32( versionBytes, 0 ).Should().Be( LogReader.CurrentStreamVersion );

                using( var reader = new LogReader( mem, LogReader.CurrentStreamVersion, 4 ) )
                {
                    reader.MoveNext().Should().BeTrue();
                    reader.Current.Text.Should().Be( e1.Text );
                    reader.Current.LogLevel.Should().Be( e1.LogLevel );
                    reader.Current.LogTime.Should().Be( e1.LogTime );
                    reader.Current.FileName.Should().Be( e1.FileName );
                    reader.Current.LineNumber.Should().Be( e1.LineNumber );
                    reader.Current.Exception.ExceptionTypeAssemblyQualifiedName.Should().Be( e1.Exception.ExceptionTypeAssemblyQualifiedName );
                    reader.Current.Exception.ToString().Should().Be( e1.Exception.ToString() );

                    reader.MoveNext().Should().BeTrue();
                    reader.CurrentMulticast.PreviousEntryType.Should().Be( LogEntryType.Line );
                    reader.CurrentMulticast.PreviousLogTime.Should().Be( prevLog );
                    reader.Current.Text.Should().Be( e2.Text );
                    reader.Current.LogTime.Should().Be( e2.LogTime );
                    reader.Current.FileName.Should().BeNull();
                    reader.Current.LineNumber.Should().Be( 0, "Since no file name is set, line number is 0." );
                    reader.Current.Exception.ExceptionTypeAssemblyQualifiedName.Should().Be( e2.Exception.ExceptionTypeAssemblyQualifiedName );
                    reader.Current.Exception.ToString().Should().Be( e2.Exception.ToString() );
                    
                    reader.MoveNext().Should().BeFalse();
                    reader.BadEndOfFileMarker.Should().BeFalse();
                }
            }

        }

    }
}
