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

        string GetSourceConfig( LogFilter globalDefaultFilter ) => @"{ ""Monitoring"": {
            ""GlobalDefaultFilter"": ""XXX"",
            ""GrandOutput"": {
                ""Handlers"": {
                    ""CK.Monitoring.Hosting.Tests.DemoSinkHandler, CK.Monitoring.Hosting.Tests"": true
                }
            }
        } }".Replace( "XXX", globalDefaultFilter.ToString() );


        [Test]
        public async Task GlobalDefaultFilter_configuration_works()
        {
            DemoSinkHandler.Reset();
            var json = new DynamicJsonConfigurationSource( GetSourceConfig( LogFilter.Debug ) );
            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, config ) => config.Add( json ) )
                        .UseMonitoring()
                        .Build();
            await host.StartAsync();
            var m = new ActivityMonitor( "The topic!" );
            m.MinimalFilter.Should().Be( LogFilter.Undefined );
            m.ActualFilter.Should().Be( LogFilter.Undefined );
            ActivityMonitor.DefaultFilter.Should().Be( LogFilter.Debug );
            m.Debug( "Hop!" );
            await host.StopAsync();
            DemoSinkHandler.LogEvents.Select( e => e.Entry.Text ).Should().Contain( "Topic: The topic!" ).And.Contain( "Hop!" );
        }
    }
}
