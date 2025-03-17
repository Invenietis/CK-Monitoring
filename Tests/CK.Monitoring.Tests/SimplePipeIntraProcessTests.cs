using CK.Core;
using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using System.Threading;
using CK.Monitoring.InterProcess;
using System.Threading.Tasks;

namespace CK.Monitoring.Tests;

[TestFixture]
public class SimplePipeIntraProcessTests
{
    [Test]
    public async Task sending_log_from_client_Async()
    {
        string logPath = TestHelper.PrepareLogFolder( "sending_log_from_client" );
        var c = new GrandOutputConfiguration()
                        .AddHandler( new Handlers.TextFileConfiguration() { Path = logPath } )
                        .AddHandler( new Handlers.BinaryFileConfiguration() { Path = logPath } );
        await using( var g = new GrandOutput( c ) )
        {
            var m = new ActivityMonitor( ActivityMonitorOptions.SkipAutoConfiguration );
            g.EnsureGrandOutputClient( m );
            var txt = new StupidStringClient();
            m.Output.RegisterClient( txt );
            using( var r = SimpleLogPipeReceiver.Start( m, interProcess: false ) )
            {
                RunClient( r.PipeName );
                r.WaitEnd( false ).ShouldBe( LogReceiverEndStatus.Normal );
            }
            var stupidLogs = txt.ToString();
            stupidLogs.ShouldContain( "From client." )
                      .ShouldContain( "An Exception for the fun." )
                      // StupidStringClient does not dump inner exception, only the top message.
                      // .And.Contain( "With an inner exception!" )
                      .ShouldContain( "Info n°0" )
                      .ShouldContain( "Info n°19" );
        }
        // All temporary files have been closed.
        var fileNames = Directory.EnumerateFiles( logPath ).ToList();
        fileNames.ShouldNotContain( s => s.EndsWith( ".tmp" ) );
        // Brutality here: opening the binary file as a text.
        // It is enough to check the serialized strings.
        var texts = fileNames.Select( n => File.ReadAllText( n ) );
        //foreach( var logs in texts )
        //{
        //    logs.ShouldContain( Encoding.ASCII.GetBytes( "From client." ) )
        //                 .And.Contain( Encoding.ASCII.GetBytes( "An Exception for the fun." ) )
        //                 .And.Contain( Encoding.ASCII.GetBytes( "With an inner exception!" ) )
        //                 .And.Contain( Encoding.ASCII.GetBytes( "Info n°0" ) )
        //                 .And.Contain( Encoding.ASCII.GetBytes( "Info n°19" ) );
        //}
    }

    void RunClient( string pipeHandlerName )
    {
        var m = new ActivityMonitor( ActivityMonitorOptions.SkipAutoConfiguration );
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
