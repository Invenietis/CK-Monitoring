using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using System.Threading.Tasks;
using FluentAssertions;
using System.Threading;
using CK.Text;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class TextFileTests
    {
        static readonly Exception _exceptionWithInner;
        static readonly Exception _exceptionWithInnerLoader;

        #region static initialization of exceptions
        static TextFileTests()
        {
            _exceptionWithInner = ThrowExceptionWithInner( false );
            _exceptionWithInnerLoader = ThrowExceptionWithInner( true );
        }

        static Exception ThrowExceptionWithInner( bool loaderException = false )
        {
            Exception e;
            try { throw new Exception( "Outer", loaderException ? ThrowLoaderException() : ThrowSimpleException( "Inner" ) ); }
            catch( Exception ex ) { e = ex; }
            return e;
        }

        static Exception ThrowSimpleException( string message )
        {
            Exception e;
            try { throw new Exception( message ); }
            catch( Exception ex ) { e = ex; }
            return e;
        }

        static Exception ThrowLoaderException()
        {
            Exception e = null;
            try { Type.GetType( "A.Type, An.Unexisting.Assembly", true ); }
            catch( Exception ex ) { e = ex; }
            return e;
        }
        #endregion

        [SetUp]
        public void InitializePath() => TestHelper.InitalizePaths();

        [Test]
        public void text_file_auto_flush_and_reconfiguration()
        {
            string folder = TestHelper.PrepareLogFolder( "AutoFlush" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "AutoFlush" };
            textConf.AutoFlushRate.Should().Be( 6, "Default AutoFlushRate configuration." );

            var config = new GrandOutputConfiguration().AddHandler( textConf );
            config.TimerDuration.Should().Be( TimeSpan.FromMilliseconds( 500 ), "Default timer congiguration." );

            using( GrandOutput g = new GrandOutput( config ) )
            {
                var m = new ActivityMonitor( false );
                g.EnsureGrandOutputClient( m );
                Thread.Sleep( 5 );
                m.Info( "Must wait 3 seconds..." );
                Thread.Sleep( 700 );
                string tempFile = Directory.EnumerateFiles( folder ).Single();
                TestHelper.FileReadAllText( tempFile ).Should().BeEmpty();
                Thread.Sleep( 3000 );
                TestHelper.FileReadAllText( tempFile ).Should().Contain( "Must wait 3 seconds..." );

                textConf.AutoFlushRate = 1;
                m.Info( "Reconfiguration triggers a flush..." );
                Thread.Sleep( 10 );
                g.ApplyConfiguration( new GrandOutputConfiguration().AddHandler( textConf ), waitForApplication: true );
                TestHelper.FileReadAllText( tempFile ).Should().Contain( "Reconfiguration triggers a flush..." );
                m.Info( "Wait only approx 500ms..." );
                Thread.Sleep( 700 );
                string final = TestHelper.FileReadAllText( tempFile );
                final.Should().Contain( "Wait only approx 500ms" );
            }
        }

        [Test]
        public void external_logs_quick_test()
        {
            string folder = TestHelper.PrepareLogFolder( "ExternalLogsQuickTest" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "ExternalLogsQuickTest" };
            var config = new GrandOutputConfiguration().AddHandler( textConf );
            using( GrandOutput g = new GrandOutput( config ) )
            {
                Task.Run( () => g.ExternalLog( LogLevel.Info, "Async started." ) ).Wait();
                var m = new ActivityMonitor( false );
                g.EnsureGrandOutputClient( m );
                m.Info( "Normal monitor starts." );
                Task t = Task.Run( () =>
                {
                    for( int i = 0; i < 10; ++i ) g.ExternalLog( LogLevel.Info, $"Async n°{i}." );
                } );
                m.MonitorEnd( "This is the end." );
                t.Wait();
            }
            string textLogged = File.ReadAllText( Directory.EnumerateFiles( folder ).Single() );
            textLogged.Should()
                        .Contain( "Normal monitor starts." )
                        .And.Contain( "Async started." )
                        .And.Contain( "Async n°0." )
                        .And.Contain( "Async n°9." )
                        .And.Contain( "This is the end." );
        }

        [Test]
        public void external_logs_stress_test()
        {
            string folder = TestHelper.PrepareLogFolder( "ExternalLogsStressTest" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "ExternalLogsStressTest" };
            var config = new GrandOutputConfiguration().AddHandler( textConf );
            int taskCount = 20;
            int logCount = 10;
            using( GrandOutput g = new GrandOutput( config ) )
            {
                var tasks = Enumerable.Range( 0, taskCount ).Select( c => Task.Run( () =>
                 {
                     for( int i = 0; i < logCount; ++i )
                     {
                         Thread.Sleep( 2 );
                         g.ExternalLog( LogLevel.Info, $"{c} n°{i}." );
                     }
                 } ) ).ToArray();
                Task.WaitAll( tasks );
            }
            string textLogged = File.ReadAllText( Directory.EnumerateFiles( folder ).Single() );
            for( int c = 0; c < taskCount; ++c )
                for( int i = 0; i < logCount; ++i )
                    textLogged.Should()
                        .Contain( $"{c} n°{i}." );
        }

        [Test]
        public void external_logs_filtering()
        {
            string folder = TestHelper.PrepareLogFolder( "ExternalLogsFiltering" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "ExternalLogsFiltering" };
            var config = new GrandOutputConfiguration().AddHandler( textConf );
            ActivityMonitor.DefaultFilter.Line.Should().Be( LogLevelFilter.Trace );
            using( GrandOutput g = new GrandOutput( config ) )
            {
                g.ExternalLog( LogLevel.Debug, "NOSHOW" );
                g.ExternalLog( LogLevel.Trace, "SHOW 0" );
                g.ExternalLogFilter = LogLevelFilter.Debug;
                g.ExternalLog( LogLevel.Debug, "SHOW 1" );
                g.ExternalLogFilter = LogLevelFilter.Error;
                g.ExternalLog( LogLevel.Warn, "NOSHOW" );
                g.ExternalLog( LogLevel.Error, "SHOW 2" );
                g.ExternalLog( LogLevel.Fatal, "SHOW 3" );
                g.ExternalLog( LogLevel.Trace | LogLevel.IsFiltered, "SHOW 4" );
                g.ExternalLogFilter = LogLevelFilter.None;
                g.ExternalLog( LogLevel.Debug, "NOSHOW" );
                g.ExternalLog( LogLevel.Trace, "SHOW 4" );
            }
            string textLogged = File.ReadAllText( Directory.EnumerateFiles( folder ).Single() );
            textLogged.Should()
                        .Contain( "SHOW 0" )
                        .And.Contain( "SHOW 1" )
                        .And.Contain( "SHOW 2" )
                        .And.Contain( "SHOW 3" )
                        .And.Contain( "SHOW 4" )
                        .And.NotContain( "NOSHOW" );
        }

        [Explicit]
        [Test]
        public void dumping_text_file_with_multiple_monitors()
        {
            string folder = TestHelper.PrepareLogFolder( "TextFileMulti" );
            Random r = new Random();
            GrandOutputConfiguration config = new GrandOutputConfiguration()
                                                    .AddHandler( new Handlers.TextFileConfiguration() { Path = "TextFileMulti" } );
            using( GrandOutput g = new GrandOutput( config ) )
            {
                Parallel.Invoke(
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs2( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs2( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs1( r, g ),
                    () => DumpSampleLogs2( r, g )
                    );
            }
            FileInfo f = new DirectoryInfo( LogFile.RootLogPath + "TextFileMulti" ).EnumerateFiles().Single();
            string text = File.ReadAllText( f.FullName );
            Console.WriteLine( text );
        }

        [Test]
        public void dumping_text_file()
        {
            string folder = TestHelper.PrepareLogFolder( "TextFile" );
            Random r = new Random();
            GrandOutputConfiguration config = new GrandOutputConfiguration()
                                                    .AddHandler( new Handlers.TextFileConfiguration() { Path = "TextFile" } );
            using( GrandOutput g = new GrandOutput( config ) )
            {
                DumpSampleLogs1( r, g );
                DumpSampleLogs2( r, g );
            }
            FileInfo f = new DirectoryInfo( LogFile.RootLogPath + "TextFile" ).EnumerateFiles().Single();
            string text = File.ReadAllText( f.FullName );
            Console.WriteLine( text );
            text.Should().Contain( "First Activity..." );
            text.Should().Contain( "End of first activity." );
            text.Should().Contain( "another one" );
            text.Should().Contain( "Something must be said" );
            text.Should().Contain( "My very first conclusion." );
            text.Should().Contain( "My second conclusion." );
        }

        [Test]
        public void text_file_auto_delete_by_date()
        {
            string folder = TestHelper.PrepareLogFolder( "AutoDelete_Date" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "AutoDelete_Date" };
            textConf.HousekeepingRate.Should().Be( 1800, "Default HousekeepingRate configuration" );
            textConf.MinimumDaysToKeep.Should().Be( 60, "Default HousekeepingRate configuration" );
            textConf.MinimumTimeSpanToKeep.Should().Be( TimeSpan.FromDays( 60 ), "Default HousekeepingRate configuration" );
            textConf.MaximumTotalKbToKeep.Should().Be( 100_000, "Default HousekeepingRate configuration" );

            // Change configuration for tests
            textConf.HousekeepingRate = 1; // Run every 500ms
            textConf.MaximumTotalKbToKeep = 0; // Always delete file beyond max size
            textConf.MinimumTimeSpanToKeep = TimeSpan.FromSeconds( 3 ); // Delete files older than 3 seconds
            var config = new GrandOutputConfiguration().AddHandler( textConf );

            // TEST DELETION BY DATE

            using( GrandOutput g = new GrandOutput( config ) )
            {
                var m = new ActivityMonitor( false );
                g.EnsureGrandOutputClient( m );
                Thread.Sleep( 5 );
                m.Info( "Hello world" );
                Thread.Sleep( 700 );

                string tempFile = Directory.EnumerateFiles( folder ).Single();
                File.Exists( tempFile ).Should().BeTrue( "Log file was created and exists" );

                // Wait for next flush (500ms), and deletion threshold (3000ms)
                Thread.Sleep( 4000 );

                File.Exists( tempFile ).Should().BeTrue( "Log file wasn't deleted yet - it's still active" );
            }
            string finalLogFile = Directory.EnumerateFiles( folder ).Single();

            // Open another GrandOutput to trigger housekeeping
            using( GrandOutput g = new GrandOutput( config ) )
            {
                // Wait for next flush (500 ms)
                Thread.Sleep( 600 );
            }

            File.Exists( finalLogFile ).Should().BeFalse( "Inactive log file was deleted" );

            // TEST DELETION BY FILE SIZE

        }

        [Test]
        public void text_file_auto_delete_by_size()
        {
            string folder = TestHelper.PrepareLogFolder( "AutoDelete_Size" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "AutoDelete_Size" };
            // Change configuration for tests
            textConf.HousekeepingRate = 1; // Run every 500ms
            textConf.MaximumTotalKbToKeep = 1; // Always delete file beyond max size
            textConf.MinimumTimeSpanToKeep = TimeSpan.Zero; // Make minimum timespan
            var config = new GrandOutputConfiguration().AddHandler( textConf );

            int lineLengthToLogToGet1000bytes = 1000 - 413;

            // TEST DELETION BY SIZE

            // Create 3*1 KB log files
            for( int i = 0; i < 3; i++ )
            {
                using( GrandOutput g = new GrandOutput( config ) )
                {
                    var m = new ActivityMonitor( false );
                    g.EnsureGrandOutputClient( m );
                    m.Info( new string( 'X', lineLengthToLogToGet1000bytes ) );
                }
            }

            long getTotalLogSize()
            {
                return Directory.EnumerateFiles( folder ).Sum( x => new FileInfo( x ).Length );
            }

            var totalLogSize = getTotalLogSize();
            totalLogSize.Should().Be( 3000 );

            // Open another GrandOutput to trigger housekeeping.
            // Note: this DOES create a file!
            using( GrandOutput g = new GrandOutput( config ) )
            {
                // Wait for next flush (500 ms)
                Thread.Sleep( 600 );
            }
            var files = Directory.GetFiles( folder ).Select( f => Path.GetFileName( f ) );
            files.Should().HaveCount( 2, $"Only 2 files should be kept - the last log file, and 1x1KB file: {files.Concatenate()}" );
        }

        static void DumpSampleLogs1( Random r, GrandOutput g )
        {
            var m = new ActivityMonitor( false );
            g.EnsureGrandOutputClient( m );
            m.SetTopic( "First Activity..." );
            if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
            using( m.OpenTrace( "Opening trace" ) )
            {
                if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
                m.Trace( "A trace in group." );
                if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
                m.Info( "An info in group." );
                if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
                m.Warn( "A warning in group." );
                if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
                m.Error( "An error in group." );
                if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
                m.Fatal( "A fatal in group." );
            }
            if( r.Next( 3 ) == 0 ) System.Threading.Thread.Sleep( 100 + r.Next( 2500 ) );
            m.Trace( "End of first activity." );
        }

        static void DumpSampleLogs2( Random r, GrandOutput g )
        {
            var m = new ActivityMonitor( false );
            g.EnsureGrandOutputClient( m );

            m.Fatal( "An error occured", _exceptionWithInner );
            m.Fatal( "Same error occured (wrapped in CKException)", new CKException( CKExceptionData.CreateFrom( _exceptionWithInner ) ) );
            m.SetTopic( "This is a topic..." );
            m.Trace( "a trace" );
            m.Trace( "another one" );
            m.SetTopic( "Please, show this topic!" );
            m.Trace( "Anotther trace." );
            using( m.OpenTrace( "A group trace." ) )
            {
                m.Trace( "A trace in group." );
                m.Info( "An info..." );
                using( m.OpenInfo( @"A group information... with a 
multi
-line
message. 
This MUST be correctly indented!" ) )
                {
                    m.Info( "Info in info group." );
                    m.Info( "Another info in info group." );
                    m.Error( "An error.", _exceptionWithInnerLoader );
                    m.Error( "Same error occured (wrapped in CKException)", new CKException( CKExceptionData.CreateFrom( _exceptionWithInnerLoader ) ) );
                    m.Warn( "A warning." );
                    m.Trace( "Something must be said." );
                    m.CloseGroup( "Everything is in place." );
                }
            }
            m.SetTopic( null );
            using( m.OpenTrace( "A group with multiple conclusions." ) )
            {
                using( m.OpenTrace( "A group with no conclusion." ) )
                {
                    m.Trace( "Something must be said." );
                }
                m.CloseGroup( new[] {
                        new ActivityLogGroupConclusion( "My very first conclusion." ),
                        new ActivityLogGroupConclusion( "My second conclusion." ),
                        new ActivityLogGroupConclusion( @"My very last conclusion
is a multi line one.
and this is fine!" )
                    } );
            }
            m.Trace( "This is the final trace." );
        }

    }
}
