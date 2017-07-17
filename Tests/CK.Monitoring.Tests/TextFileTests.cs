using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using System.Threading.Tasks;
using FluentAssertions;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class TextFileTests
    {
        static readonly Exception _exceptionWithInner;
        static readonly Exception _exceptionWithInnerLoader;

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

        [SetUp]
        public void InitializePath() => TestHelper.InitalizePaths();

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
            FileInfo f = new DirectoryInfo( SystemActivityMonitor.RootLogPath + "TextFileMulti" ).EnumerateFiles().Single();
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
            FileInfo f = new DirectoryInfo( SystemActivityMonitor.RootLogPath + "TextFile" ).EnumerateFiles().Single();
            string text = File.ReadAllText( f.FullName );
            Console.WriteLine( text );
            text.Should().Contain( "First Activity..." );
            text.Should().Contain( "End of first activity." );
            text.Should().Contain( "another one" );
            text.Should().Contain( "Something must be said" );
            text.Should().Contain( "My very first conclusion." );
            text.Should().Contain( "My second conclusion." );
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
