using CK.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core.Impl;
using System.IO;
using CK.Monitoring;
using System.Diagnostics;
using System.Threading;
using FluentAssertions;
using CK.Monitoring.InterProcess;

namespace CK.Monitoring.Tests
{
    // This fails with NUnit 2.6.4 GUI runner:
    // A SafeHandle destructor raises an exception during Garbage Collection.
#if !NET461
    [TestFixture]
    public class AnonymousPipeIntraProcessTests
    {
        [Test]
        public void sending_log_from_client()
        {
            var m = new ActivityMonitor();
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
            var logs = txt.ToString();
            logs.Should().Contain( "From client." )
                         .And.Contain( "An Exception for the fun." )
                         .And.Contain( "Info n°0" )
                         .And.Contain( "Info n°19" );
        }

        void RunClient( string pipeHandlerName )
        {
            var m = new ActivityMonitor( false );
            using( var pipe = new SimplePipeSenderActivityMonitorClient( pipeHandlerName ) )
            {
                m.Output.RegisterClient( pipe );
                using( m.OpenInfo( "From client." ) )
                {
                    m.Fatal( "A fatal.", new Exception( "An Exception for the fun." ) );
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
