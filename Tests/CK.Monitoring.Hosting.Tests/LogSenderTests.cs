using CK.AspNet.Tester;
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting.Tests
{
    [TestFixture]
    public class LogSenderTests
    {
        public async Task<IHost> StartHostAsync()
        {
            var config = new DynamicConfigurationSource();
            config["CK-Monitoring:GrandOutput:Handlers:CK.Monitoring.Hosting.Tests.FakeLogSender, CK.Monitoring.Hosting.Tests"] = "true";
            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                        .UseCKMonitoring()
                        .Build();
            await host.StartAsync();
            return host;
        }

        [Test]
        public async Task creating_sender_waits_for_SenderCanBeCreated_and_Sender_is_Disposed_Async()
        {
            FakeLogSender.Reset();
            var host = await StartHostAsync();
            Debug.Assert( FakeLogSender.ActivatedSender != null );

            var monitor = new ActivityMonitor() { AutoTags = FakeLogSender.TestTag };
            monitor.Info( "NOSHOW" );
            await Task.Delay( 50 );
            FakeLogSender.ActivatedSender.Should().NotBeNull();
            FakeLogSender.ActivatedSender.FakeSender.Should().BeNull();
            FakeLogSender.FakeSenderCanBeCreated = true;
            monitor.Info( "NOSHOW" );
            await Task.Delay( 50 );
            var sender = FakeLogSender.ActivatedSender.FakeSender;
            Debug.Assert( sender != null );
            sender.Disposed.Should().BeFalse();

            FakeLogSender.LogSent.Should().BeEmpty( "FakeSenderIsActuallyConnected is false: no log can be sent." );

            await host.StopAsync();
            sender.Disposed.Should().BeTrue();
        }

        [Test]
        public async Task waiting_for_ISender_creation_buffers_the_logs_Async()
        {
            FakeLogSender.Reset();
            var host = await StartHostAsync();
            Debug.Assert( FakeLogSender.ActivatedSender != null, "The handler is activated." );

            var monitor = new ActivityMonitor() { AutoTags = FakeLogSender.TestTag };
            monitor.Info( "NOSHOW since the buffer is configured to 5 and we'll buffer 6 logs here." );
            await Task.Delay( 50 );

            monitor.Info( "n°1" );
            monitor.Info( "n°2" );
            monitor.Info( "n°3" );
            monitor.Info( "n°4" );
            // This one will be buffered and evict the NOSHOW.
            monitor.Info( "n°5" );
            // Let the logs reach the handler.
            await Task.Delay( 50 );

            // Allow the ISender to be created and actually send the logs.
            FakeLogSender.FakeSenderCanBeCreated = true;
            FakeLogSender.FakeSenderImplIsActuallyConnected = true;
            FakeLogSender.FakeSenderImplTrySendSuccess = true;

            // One log will now trigger the creation of the ISender, the
            // dump of the buffered entries and the n°6.
            monitor.Info( "n°6" );
            await Task.Delay( 50 );

            var sender = FakeLogSender.ActivatedSender.FakeSender;
            Debug.Assert( sender != null, "The sender has been created." );

            FakeLogSender.LogSent.Concatenate().Should().Contain( "n°1, n°2, n°3, n°4, n°5, n°6" ).And.NotContain( "NOSHOW" );

            await host.StopAsync();
        }

        [Test]
        public async Task persistent_error_for_sender_prevents_handler_activation_Async()
        {
            FakeLogSender.Reset();
            FakeLogSender.FakeSenderCanBeCreated = true;
            FakeLogSender.FakeSenderPersistentError = true;

            var host = await StartHostAsync();
            Debug.Assert( FakeLogSender.ActivatedSender == null );

            await host.StopAsync();
        }

        [Test]
        public async Task persistent_error_for_sender_deactivate_the_handler_Async()
        {
            FakeLogSender.Reset();
            // Let FakeLogSender.FakeSenderCanBeCreated = false: the handler is activated
            // but cannot create the sender yet.
            FakeLogSender.FakeSenderPersistentError = true;

            var host = await StartHostAsync();
            Debug.Assert( FakeLogSender.ActivatedSender != null );
            FakeLogSender.ActivatedSender.FakeSender.Should().BeNull();

            // Conditions to create the sender becomes true.
            FakeLogSender.FakeSenderCanBeCreated = true;

            // Handling a log entry triggers the sender creation but the "persistent error"
            // throws an exception: the handler is condemned (deactivation and removed).
            var monitor = new ActivityMonitor() { AutoTags = FakeLogSender.TestTag };
            monitor.Info( "NOSHOW" );
            await Task.Delay( 50 );

            FakeLogSender.ActivatedSender.Should().BeNull( "The handler has been deactivated and removed." );

            await host.StopAsync();
        }


        [TestCase( "UseIsActuallyConnected" )]
        [TestCase( "UseImplTrySendSuccess" )]
        public async Task ISender_buffers_the_logs_while_connection_is_lost_Async( string mode )
        {
            void Open( bool open )
            {
                if( mode == "UseIsActuallyConnected" )
                {
                    FakeLogSender.FakeSenderImplIsActuallyConnected = open;
                    FakeLogSender.FakeSenderImplTrySendSuccess = true;
                }
                else
                {
                    FakeLogSender.FakeSenderImplIsActuallyConnected = true;
                    FakeLogSender.FakeSenderImplTrySendSuccess = open;
                }
            }

            FakeLogSender.Reset();
            FakeLogSender.FakeSenderCanBeCreated = true;
            Open( true );
            var host = await StartHostAsync();
            Debug.Assert( FakeLogSender.ActivatedSender != null, "The handler is activated." );

            var monitor = new ActivityMonitor() { AutoTags = FakeLogSender.TestTag };
            monitor.Info( "n°1" );
            await Task.Delay( 50 );
            Open( false );

            // The LostBufferSize is 3: these 3 will be eventually sent.
            monitor.Info( "n°2" );
            monitor.Info( "n°3" );
            monitor.Info( "n°4" );
            await Task.Delay( 50 );

            Open( true );
            monitor.Info( "n°5" );
            await Task.Delay( 50 );

            Open( false );
            monitor.Info( "NOSHOW - will be evicted." );
            monitor.Info( "n°6" );
            monitor.Info( "n°7" );
            monitor.Info( "n°8" );
            await Task.Delay( 50 );

            Open( true );
            monitor.Info( "n°9" );
            await Task.Delay( 50 );

            FakeLogSender.LogSent.Concatenate().Should().Contain( "n°1, n°2, n°3, n°4, n°5, n°6, n°7, n°8, n°9" ).And.NotContain( "NOSHOW" );

            await host.StopAsync();
        }

    }
}
