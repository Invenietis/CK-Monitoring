using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;
using System.Threading;
using System.Text.RegularExpressions;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class GrandOutputTests
    {
        [SetUp]
        public void InitalizePaths() => TestHelper.InitalizePaths();

        [Test]
        public void CKMon_binary_files_can_be_GZip_compressed()
        {
            string folder = TestHelper.PrepareLogFolder( "Gzip" );

            var c = new GrandOutputConfiguration()
                            .AddHandler( new Handlers.BinaryFileConfiguration()
                            {
                                Path = folder + @"\OutputGzip",
                                UseGzipCompression = true
                            } )
                            .AddHandler( new Handlers.BinaryFileConfiguration()
                            {
                                Path = folder + @"\OutputRaw",
                                UseGzipCompression = false
                            } );

            using( GrandOutput g = new GrandOutput( c ) )
            {
                var taskA = Task.Factory.StartNew( () => DumpMonitor1082Entries( CreateMonitorAndRegisterGrandOutput( "Task A", g ), 5 ), TaskCreationOptions.LongRunning );
                var taskB = Task.Factory.StartNew( () => DumpMonitor1082Entries( CreateMonitorAndRegisterGrandOutput( "Task B", g ), 5 ), TaskCreationOptions.LongRunning );
                var taskC = Task.Factory.StartNew( () => DumpMonitor1082Entries( CreateMonitorAndRegisterGrandOutput( "Task C", g ), 5 ), TaskCreationOptions.LongRunning );

                Task.WaitAll( taskA, taskB, taskC );
            }

            string[] gzipCkmons = TestHelper.WaitForCkmonFilesInDirectory( folder + @"\OutputGzip", 1 );
            string[] rawCkmons = TestHelper.WaitForCkmonFilesInDirectory( folder + @"\OutputRaw", 1 );

            gzipCkmons.Should().HaveCount( 1 );
            rawCkmons.Should().HaveCount( 1 );

            FileInfo gzipCkmonFile = new FileInfo( gzipCkmons.Single() );
            FileInfo rawCkmonFile = new FileInfo( rawCkmons.Single() );

            gzipCkmonFile.Exists.Should().BeTrue();
            rawCkmonFile.Exists.Should().BeTrue();

            // Test file size
            gzipCkmonFile.Length.Should().BeLessThan( rawCkmonFile.Length );

            // Test de-duplication between Gzip and non-Gzip
            MultiLogReader mlr = new MultiLogReader();
            var fileList = mlr.Add( new string[] { gzipCkmonFile.FullName, rawCkmonFile.FullName } );
            fileList.Should().HaveCount( 2 );

            var map = mlr.GetActivityMap();

            map.Monitors.Should().HaveCount( 3 );
            map.Monitors[0].ReadFirstPage( 6000 ).Entries.Should().HaveCount( 5415 );
            map.Monitors[1].ReadFirstPage( 6000 ).Entries.Should().HaveCount( 5415 );
            map.Monitors[2].ReadFirstPage( 6000 ).Entries.Should().HaveCount( 5415 );
        }

        static IActivityMonitor CreateMonitorAndRegisterGrandOutput( string topic, GrandOutput go )
        {
            var m = new ActivityMonitor( applyAutoConfigurations: false, topic: topic );
            go.EnsureGrandOutputClient( m );
            return m;
        }

        [Test]
        public void disposing_GrandOutput_waits_for_termination()
        {
            string logPath = TestHelper.PrepareLogFolder( "Termination" );
            var c = new GrandOutputConfiguration()
                            .AddHandler( new Handlers.TextFileConfiguration() { Path = logPath } )
                            .AddHandler( new Handlers.BinaryFileConfiguration() { Path = logPath } );
            using( var g = new GrandOutput( c ) )
            {
                var m = new ActivityMonitor( applyAutoConfigurations: false );
                g.EnsureGrandOutputClient( m );
                DumpMonitor1082Entries( m, 300 );
            }
            // All tempoary files have been closed.
            var fileNames = Directory.EnumerateFiles( logPath ).ToList();
            fileNames.Should().NotContain( s => s.EndsWith( ".tmp" ) );
            // The 300 "~~~~~FINAL TRACE~~~~~" appear in text logs.
            fileNames
                .Where( n => n.EndsWith( ".txt" ) )
                .Select( n => File.ReadAllText( n ) )
                .Select( t => Regex.Matches( t, "~~~~~FINAL TRACE~~~~~" ).Count )
                .Sum()
                .Should().Be( 300 );
        }

        [Test]
        public void disposing_GrandOutput_deactivate_handlers_even_when_disposing_fast_but_logs_are_lost()
        {
            string logPath = TestHelper.PrepareLogFolder( "Termination" );
            var c = new GrandOutputConfiguration()
                            .AddHandler( new Handlers.TextFileConfiguration() { Path = logPath } );
            using( var g = new GrandOutput( c ) )
            {
                var m = new ActivityMonitor( applyAutoConfigurations: false );
                g.EnsureGrandOutputClient( m );
                DumpMonitor1082Entries( m, 300 );
                g.Dispose( TestHelper.ConsoleMonitor, 0 );
            }
            // All tempoary files have been closed.
            var fileNames = Directory.EnumerateFiles( logPath ).ToList();
            fileNames.Should().NotContain( s => s.EndsWith( ".tmp" ) );
            // There is less that the normal 300 "~~~~~FINAL TRACE~~~~~" in text logs.
            fileNames
                .Where( n => n.EndsWith( ".txt" ) )
                .Select( n => File.ReadAllText( n ) )
                .Select( t => Regex.Matches( t, "~~~~~FINAL TRACE~~~~~" ).Count )
                .Sum()
                .Should().BeLessThan( 300 );
        }

        static void DumpMonitor1082Entries( IActivityMonitor monitor, int count )
        {
            Exception exception1;
            Exception exception2;

            try
            {
                throw new InvalidOperationException( "Exception!" );
            }
            catch( Exception e )
            {
                exception1 = e;
            }

            try
            {
                throw new InvalidOperationException( "Inception!", exception1 );
            }
            catch( Exception e )
            {
                exception2 = e;
            }
            const int nbLoop = 180;
            // Entry count per count = 3 + 180 * 6 = 1083
            // Entry count (for count parameter = 5): 5415
            //      Since there is 3 parallel activities this fits into the 
            //      default per file count of 20000.
            for( int i = 0; i < count; i++ )
            {
                using( monitor.OpenTrace( $"Dump output loop {i}" ) )
                {
                    for( int j = 0; j < nbLoop; j++ )
                    {
                        // Debug is not sent.
                        monitor.Debug( $"Debug log! {j}" );
                        monitor.Trace( $"Trace log! {j}" );
                        monitor.Info( $"Info log! {j}" );
                        monitor.Warn( $"Warn log! {j}" );
                        monitor.Error( $"Error log! {j}" );
                        monitor.Error( $"Fatal log! {j}" );
                        monitor.Error( "Exception log! {j}", exception2 );
                    }
                }
                monitor.Info( "~~~~~FINAL TRACE~~~~~" );
            }
        }

    }
}
