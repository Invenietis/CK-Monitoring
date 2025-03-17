using System;
using System.Diagnostics;
using System.Linq;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring.Tests.Persistence;

[TestFixture]
public class MultiFileReaderTests
{
    [SetUp]
    public void InitalizePaths()
    {
        TestHelper.InitalizePaths();
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    [TearDown]
    public void WaitForNoMoreAliveInputLogEntry()
    {
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    [TestCase( false )]
    [TestCase( true )]
    [Explicit( "Buggy. To be fixed." )]
    public async Task duplicates_are_automatically_removed_Async( bool useGzipFormat )
    {
        Stopwatch sw = new Stopwatch();
        for( int nbEntries1 = 1; nbEntries1 < 8; ++nbEntries1 )
            for( int nbEntries2 = 1; nbEntries2 < 8; ++nbEntries2 )
            {
                TestHelper.ConsoleMonitor.Trace( $"Start {nbEntries1}/{nbEntries2}." );
                sw.Restart();
                await DuplicateTestWith6Entries_Async( nbEntries1, nbEntries2, useGzipFormat );
                TestHelper.ConsoleMonitor.Trace( $"Done in {sw.Elapsed}." );
            }
    }

    static async Task DuplicateTestWith6Entries_Async( int nbEntries1, int nbEntries2, bool gzip = false )
    {
        var folder = TestHelper.PrepareLogFolder( "ReadDuplicates" );

        var config = new GrandOutputConfiguration()
                        .AddHandler( new Handlers.BinaryFileConfiguration()
                        {
                            Path = folder,
                            MaxCountPerFile = nbEntries1,
                            UseGzipCompression = gzip
                        } )
                        .AddHandler( new Handlers.BinaryFileConfiguration()
                        {
                            Path = folder,
                            MaxCountPerFile = nbEntries2,
                            UseGzipCompression = gzip
                        } );
        await using( var o = new GrandOutput( config ) )
        {
            var m = new ActivityMonitor();
            o.EnsureGrandOutputClient( m );
            var direct = m.Output.RegisterClient( new CKMonWriterClient( folder, Math.Min( nbEntries1, nbEntries2 ), LogFilter.Debug, gzip ) );
            // 6 traces that go to the GrandOutput but also to the direct CKMonWriterClient.
            m.Trace( "Trace 1" );
            m.OpenTrace( "OpenTrace 1" );
            m.Trace( "Trace 1.1" );
            m.Trace( "Trace 1.2" );
            m.CloseGroup();
            m.Trace( "Trace 2" );
            Thread.Sleep( 100 );
            m.Output.UnregisterClient( direct );
        }
        InputLogEntry.AliveCount.ShouldBe( 0 );

        var files = TestHelper.WaitForCkmonFilesInDirectory( folder, 3 );
        for( int pageReadLength = 1; pageReadLength < 10; ++pageReadLength )
        {
            using MultiLogReader reader = new MultiLogReader();
            reader.Add( files );
            var map = reader.GetActivityMap();
            map.ValidFiles.All( rawFile => rawFile.IsValidFile ).ShouldBeTrue( "All files are correctly closed with the final 0 byte and no exception occurred while reading them." );
            map.Monitors.Count.ShouldBe( 2 );

            var allEntries1 = map.Monitors[0].ReadAllEntries().ToList();
            var allEntries2 = map.Monitors[1].ReadAllEntries().ToList();

            var allEntries = allEntries1.Any( e => e.Entry.Text == "Topic: CK.Monitoring.DispatcherSink" )
                                ? allEntries2
                                : allEntries1;

            allEntries.Select( e => e.Entry.Text )
                      .ShouldBe( [ "Trace 1",
                                   "OpenTrace 1",
                                   "Trace 1.1",
                                   "Trace 1.2",
                                   null, // CloseGroup
                                   "<Missing log data>",
                                   "<Missing log data>",
                                   "Trace 2" ] );
        }
    }

}
