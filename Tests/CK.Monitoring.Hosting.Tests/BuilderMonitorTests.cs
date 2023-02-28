using CK.AspNet.Tester;
using CK.Core;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace CK.Monitoring.Hosting.Tests
{
    [TestFixture]
    public class BuilderMonitorTests
    {
        [SetUp]
        public void InitializePath()
        {
            TestHelper.InitalizePaths();
            TestHelper.WaitForNoMoreAliveInputLogEntry();
        }

        [Test]
        public void BuilderMonitor_is_always_available()
        {
            string folder = TestHelper.PrepareLogFolder( "FromBuilderMonitor" );

            // The GetBuilderMonitor() is available.
            // We don't use chaining here so we can have access to the builder from ConfigureHostConfiguration.
            var builder = new HostBuilder();
            builder.GetBuilderMonitor().Info( "This will eventually be logged!" );

            builder.ConfigureHostConfiguration( hostConfigurationBuilder =>
                {
                    // This is rarely used and: this is the very first step and the IHostBuilder or HostBuilderContext is not provided here.
                    // Using the reference above is crappy but this is just to show that the builder monitor is "here".
                    builder.GetBuilderMonitor().Info( "In ConfigureHostConfiguration." );
                } )
                .ConfigureAppConfiguration( (hostBuilderContext, configurationBuilder ) =>
                {
                    // Here we are: we can log stuff from ConfigureAppConfiguration.
                    hostBuilderContext.GetBuilderMonitor().Info( "In ConfigureAppConfiguration." );

                    // And we configure the text log output...
                    // This works because the BuilderMonitor retains and replays the logs received before the
                    // GrandOutpt and its handlers are made available.
                    var config = new DynamicConfigurationSource();
                    config["CK-Monitoring:GrandOutput:Handlers:TextFile:Path"] = "FromBuilderMonitor";
                    configurationBuilder.Add( config );
                } )
                .ConfigureHostOptions( ( hostBuilderContext, options ) =>
                {
                    hostBuilderContext.GetBuilderMonitor().Info( "In ConfigureHostOptions." );
                } )
                .ConfigureLogging( ( hostBuilderContext, options ) =>
                {
                    hostBuilderContext.GetBuilderMonitor().Info( "In ConfigureLogging." );
                } )
                .ConfigureServices( ( hostBuilderContext, options ) =>
                {
                    hostBuilderContext.GetBuilderMonitor().Info( "In ConfigureServices." );
                } )
                // Calls UseCKMonitoring last to show that we don't care of its position.
                .UseCKMonitoring();

            var host = builder.Build();
            GrandOutput.Default!.Dispose();
            var text = TestHelper.FileReadAllText( Directory.EnumerateFiles( folder ).Single() );
            text.Should().Contain( "This will eventually be logged!" );
            text.Should().Contain( "In ConfigureHostConfiguration." );
            text.Should().Contain( "In ConfigureAppConfiguration." );
            text.Should().Contain( "In ConfigureLogging." );
            text.Should().Contain( "In ConfigureServices." );
            text.Should().Contain( "In ConfigureHostOptions." );

            // Just make sure that the replay doesn't leak.
            ActivityMonitorExternalLogData.AliveCount.Should().Be( 0 );
            InputLogEntry.AliveCount.Should().Be( 0 );
        }
    }
}
