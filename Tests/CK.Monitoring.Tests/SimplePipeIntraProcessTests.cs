using CK.Core;
using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using System.Threading;
using FluentAssertions;
using CK.Monitoring.InterProcess;

namespace CK.Monitoring.Tests
{
    // This fails with NUnit 2.6.4 GUI runner:
    // A SafeHandle destructor raises an exception during Garbage Collection.
#if !NET461
    [TestFixture]
    public class SimplePipeIntraProcessTests
    {
        [Test]
        public void sending_log_from_client()
        {
            string logPath = TestHelper.PrepareLogFolder( "sending_log_from_client" );
            var c = new GrandOutputConfiguration()
                            .AddHandler( new Handlers.TextFileConfiguration() { Path = logPath } )
                            .AddHandler( new Handlers.BinaryFileConfiguration() { Path = logPath } );
            using( var g = new GrandOutput( c ) )
            {
                var m = new ActivityMonitor( false );
                g.EnsureGrandOutputClient( m );
                var txt = new StupidStringClient();
                m.Output.RegisterClient( txt );
                using( m.Output.CreateBridgeTo( TestHelper.ConsoleMonitor.Output.BridgeTarget ) )
                {
                    using( var r = SimpleLogPipeReceiver.Start( m, interProcess: false ) )
                    {
                        RunClient( r.PipeName );
                        r.WaitEnd( false ).Should().Be( LogReceiverEndStatus.Normal );
                    }
                }
                var stupidLogs = txt.ToString();
                stupidLogs.Should().Contain( "From client." )
                                   .And.Contain( "An Exception for the fun." )
                                   // StupidStringClient does not dump inner exception, only the top message.
                                   // .And.Contain( "With an inner exception!" )
                                   .And.Contain( "Info n°0" )
                                   .And.Contain( "Info n°19" );
            }
            // All tempoary files have been closed.
            var fileNames = Directory.EnumerateFiles( logPath ).ToList();
            fileNames.Should().NotContain( s => s.EndsWith( ".tmp" ) );
            // Brutallity here: opening the binary file as a text.
            // It is enough to check the serialized strings.
            var texts = fileNames.Select( n => File.ReadAllText( n ) );
            foreach( var logs in texts )
            {
                logs.Should().Contain( "From client." )
                             .And.Contain( "An Exception for the fun." )
                             .And.Contain( "With an inner exception!" )
                             .And.Contain( "Info n°0" )
                             .And.Contain( "Info n°19" );
            }
        }

        void RunClient( string pipeHandlerName )
        {
            var m = new ActivityMonitor( false );
            using( var pipe = new SimplePipeSenderActivityMonitorClient( pipeHandlerName ) )
            {
                m.Output.RegisterClient( pipe );
                using( m.OpenInfo( "From client." ) )
                {
                    m.Fatal( "A fatal.", new Exception( "An Exception for the fun.", new Exception( "With an inner exception!" ) ) );
                    for( int i = 0; i < 20; ++i )
                    {
                        Thread.Sleep( 100 );
                        m.Info( $"Info n°{i}" );
                    }
                }
                m.Output.UnregisterClient( pipe );
            }
        }
    }
#endif
}
