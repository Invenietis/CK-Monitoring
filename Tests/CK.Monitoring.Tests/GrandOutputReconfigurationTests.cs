using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CK.Monitoring.Tests
{
    public class GrandOutputReconfigurationTests
    {
        [Test]
        public void BinaryGzip_reconfiguration()
        {
            string folder = TestHelper.PrepareLogFolder( nameof( BinaryGzip_reconfiguration ) );
            var h = new Handlers.BinaryFileConfiguration()
            {
                Path = folder + @"\FirstPath",
                UseGzipCompression = false
            };
            var c = new GrandOutputConfiguration().AddHandler( h );

            var m = new ActivityMonitor( applyAutoConfigurations: false );
            using( GrandOutput g = new GrandOutput( c ) )
            {
                g.EnsureGrandOutputClient( m );

                m.Trace( "No Compression." );
                // We must ensure that the log above will use the current configuration.
                // This is by design and is a good thing: there is no causality/ordering between log emission and sink reconfigurations.
                Thread.Sleep( 100 );

                h.UseGzipCompression = true;
                g.ApplyConfiguration( c, true );
                m.Trace( "With Compression." );
                Thread.Sleep( 100 );

                h.Path = folder + @"\SecondPath";
                g.ApplyConfiguration( c, true );
                m.Trace( "With Compression (in second folder)." );
                Thread.Sleep( 100 );

                h.UseGzipCompression = false;
                g.ApplyConfiguration( c, true );
                m.Trace( "No Compression (in second folder)." );
            }
            // First file is NOT compressed, the second one is.
            var fileNamesFirst = Directory.EnumerateFiles( folder + @"\FirstPath" ).ToList();
            fileNamesFirst.Should().BeInAscendingOrder().And.HaveCount( 2 ).And.NotContain( s => s.EndsWith( ".tmp" ), "Temporary files have been closed." );
            File.ReadAllText( fileNamesFirst[0] ).Should().Contain( "No Compression." );
            File.ReadAllText( fileNamesFirst[1] ).Should().NotContain( "With Compression.", "Cannot read it in clear text since it is compressed..." );
            using( var reader = LogReader.Open( fileNamesFirst[1] ) )
            {
                reader.MoveNext().Should().BeTrue();
                reader.Current.Text.Should().Be( "With Compression." );
            }
            // First file is compressed, not the second one.
            var fileNamesSecond = Directory.EnumerateFiles( folder + @"\SecondPath" ).ToList();
            fileNamesSecond.Should().BeInAscendingOrder().And.HaveCount( 2 ).And.NotContain( s => s.EndsWith( ".tmp" ), "Temporary files have been closed." );
            File.ReadAllText( fileNamesSecond[0] ).Should().NotContain( "With Compression (in second folder).", "The fist file is compressed..." );
            // We restrict the log entries to the one of our monitor: this filters out the logs from the DispatcherSink.
            using( var reader = LogReader.Open( fileNamesSecond[0], filter: new LogReader.MulticastFilter( m ) ) )
            {
                reader.MoveNext().Should().BeTrue();
                reader.Current.Text.Should().Be( "With Compression (in second folder)." );
            }
            File.ReadAllText( fileNamesSecond[1] ).Should().Contain( "No Compression (in second folder)." );
        }
    }
}
