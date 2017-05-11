using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;
using System.Threading;

namespace CK.Monitoring.Tests.Persistence
{
    [TestFixture]
    public class MultiFileReaderTests
    {
        [SetUp]
        public void InitalizePaths() => TestHelper.InitalizePaths();

        [Test]
        public void artificially_generated_missing_log_entries_are_detected()
        {
            var folder = TestHelper.PrepareLogFolder("MissingEntries");
            var emptyConfig = new GrandOutputConfiguration();
            var binaryConfig = new GrandOutputConfiguration().AddHandler(new Handlers.BinaryFileConfiguration() { Path = "MissingEntries" });

            using (GrandOutput g = new GrandOutput(emptyConfig))
            {
                var m = new ActivityMonitor(false);
                g.EnsureGrandOutputClient(m);
                m.Trace().Send("NoShow-1");
                g.ApplyConfiguration(emptyConfig);
                m.Trace().Send("NoShow-2");
                Thread.Sleep(300);
                g.ApplyConfiguration(binaryConfig);
                Thread.Sleep(300);
                m.Trace().Send("Show-1");
                Thread.Sleep(300);
                g.ApplyConfiguration(emptyConfig);
                Thread.Sleep(300);
                m.Trace().Send("NoShow-3");
            }
            var replayed = new ActivityMonitor(false);
            var c = replayed.Output.RegisterClient(new StupidStringClient());
            TestHelper.ReplayLogs(new DirectoryInfo(folder), true, mon => replayed, TestHelper.ConsoleMonitor);
            c.Entries.Select(e => e.Text).ShouldBeEquivalentTo(new[] { "<Missing log data>", "Show-1" }, o => o.WithStrictOrdering());
        }


        [TestCase(false)]
        [TestCase(true)]
        public void duplicates_are_automatically_removed( bool useGzipFormat )
        {
            Stopwatch sw = new Stopwatch();
            for( int nbEntries1 = 1; nbEntries1 < 8; ++nbEntries1 )
                for( int nbEntries2 = 1; nbEntries2 < 8; ++nbEntries2 )
                {
                    TestHelper.ConsoleMonitor.Trace().Send( "Start {0}/{1}.", nbEntries1, nbEntries2 );
                    sw.Restart();
                    DuplicateTestWith6Entries( nbEntries1, nbEntries2, useGzipFormat);
                    TestHelper.ConsoleMonitor.Trace().Send( "Done in {0}.", sw.Elapsed );
                }
        }

        private static void DuplicateTestWith6Entries( int nbEntries1, int nbEntries2, bool gzip = false )
        {
            var folder = TestHelper.PrepareLogFolder("ReadDuplicates");

            var config = new GrandOutputConfiguration()
                            .AddHandler(new Handlers.BinaryFileConfiguration()
                            {
                                Path = folder,
                                MaxCountPerFile = nbEntries1,
                                UseGzipCompression = gzip
                            })
                            .AddHandler(new Handlers.BinaryFileConfiguration()
                            {
                                Path = folder,
                                MaxCountPerFile = nbEntries2,
                                UseGzipCompression = gzip
                            });
            using( var o = new GrandOutput( config ) )
            {
                var m = new ActivityMonitor();
                o.EnsureGrandOutputClient( m );
                var direct = m.Output.RegisterClient( new CKMonWriterClient( folder, Math.Min( nbEntries1, nbEntries2 ), LogFilter.Debug, gzip ) );
                // 6 traces that go to the GrandOutput but also to the direct CKMonWriterClient.
                m.Trace().Send( "Trace 1" );
                m.OpenTrace().Send( "OpenTrace 1" );
                m.Trace().Send( "Trace 1.1" );
                m.Trace().Send( "Trace 1.2" );
                m.CloseGroup();
                m.Trace().Send( "Trace 2" );
                System.Threading.Thread.Sleep( 100 );
                m.Output.UnregisterClient( direct );
            }
            var files = TestHelper.WaitForCkmonFilesInDirectory( folder, 3 );
            for( int pageReadLength = 1; pageReadLength < 10; ++pageReadLength )
            {
                MultiLogReader reader = new MultiLogReader();
                reader.Add( files );
                var map = reader.GetActivityMap();
                map.ValidFiles.All( rawFile => rawFile.IsValidFile ).Should().BeTrue( "All files are correctly closed with the final 0 byte and no exception occurred while reading them." );

                var readMonitor = map.Monitors.Single();

                List<ParentedLogEntry> allEntries = new List<ParentedLogEntry>();
                using( var pageReader = readMonitor.ReadFirstPage( pageReadLength ) )
                {
                    do
                    {
                        allEntries.AddRange( pageReader.Entries );
                    }
                    while( pageReader.ForwardPage() > 0 );
                }
                allEntries.Select(e => e.Entry.Text).ShouldBeEquivalentTo(
                    new[] { "Trace 1", "OpenTrace 1", "Trace 1.1", "Trace 1.2", null, "Trace 2" }, o => o.WithStrictOrdering() );
            }
        }

    }
}
