using CK.AspNet.Tester;
using CK.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting.Tests
{
    [TestFixture]
    public partial class HostingTests
    {
        [Test]
        public async Task GlobalDefaultFilter_configuration_works()
        {
            var config = new DynamicConfigurationSource();
            config["Monitoring:GlobalDefaultFilter"] = "Debug";
            config["Monitoring:GrandOutput:Handlers:CK.Monitoring.Hosting.Tests.DemoSinkHandler, CK.Monitoring.Hosting.Tests"] = "true";

            DemoSinkHandler.Reset();
            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                        .UseMonitoring()
                        .Build();
            await host.StartAsync();
            var m = new ActivityMonitor( "The topic!" );
            m.MinimalFilter.Should().Be( LogFilter.Undefined );
            m.ActualFilter.Should().Be( LogFilter.Undefined );
            ActivityMonitor.DefaultFilter.Should().Be( LogFilter.Debug );
            m.Debug( "Hop!" );

            config["Monitoring:GlobalDefaultFilter"] = "Release";

            ActivityMonitor.DefaultFilter.Should().Be( LogFilter.Release );
            m.Debug( "Not visible! (1)" );

            config["Monitoring:GlobalDefaultFilter"] = "Terse";
            ActivityMonitor.DefaultFilter.Should().Be( LogFilter.Terse );
            m.Debug( "Not visible! (2)" );

            config["Monitoring:GlobalDefaultFilter"] = "Debug";
            ActivityMonitor.DefaultFilter.Should().Be( LogFilter.Debug, "The global ActivityMonitor.DefaultFilter is Debug again." );
            m.Debug( "Back in game." );

            await host.StopAsync();

            DemoSinkHandler.LogEvents.Select( e => e.Entry.Text ).Should()
                   .Contain( "Topic: The topic!" )
                   .And.Contain( "Hop!" )
                   .And.NotContain( "Not visible! (1)" )
                   .And.NotContain( "Not visible! (2)" )
                   .And.Contain( "Back in game." );
        }


        [Test]
        public void GrandOutput_MinimalFilter_configuration_works()
        {
            var config = new DynamicConfigurationSource();

            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                        .UseMonitoring()
                        .Build();

            var m = new ActivityMonitor();
            m.ActualFilter.Should().Be( LogFilter.Undefined, "Initially Undefined." );

            config["Monitoring:GrandOutput:MinimalFilter"] = "Debug";

            System.Threading.Thread.Sleep( 100 );
            m.ActualFilter.Should().Be( LogFilter.Debug, "First Debug applied." );

            config["Monitoring:GrandOutput:MinimalFilter"] = "{Off,Debug}";
            System.Threading.Thread.Sleep( 100 );
            m.ActualFilter.Should().Be( new LogFilter( LogLevelFilter.Off, LogLevelFilter.Debug ), "Explicit {Off,Debug} filter." );

            config["Monitoring:GrandOutput:MinimalFilter"] = null;
            System.Threading.Thread.Sleep( 100 );
            m.ActualFilter.Should().Be( new LogFilter( LogLevelFilter.Off, LogLevelFilter.Debug ), "Null doesn't change anything." );

        }

    }
}
