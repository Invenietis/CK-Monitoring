using CK.AspNet.Tester;
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting.Tests;


[TestFixture]
[Obsolete( "HostBuilder should not be used anymore.", false )]
public partial class ObsoleteHostingTests
{

    [Test]
    public void IParallelLogger_is_the_IActivityMonitor_one_and_ActivityMonitor_is_not_exposed_by_the_DI()
    {
        var host = new HostBuilder()
                    .UseCKMonitoring()
                    .Build();

        host.Services.GetService<ActivityMonitor>().Should().BeNull();
        var ia = host.Services.GetRequiredService<IActivityMonitor>();
        var ip = host.Services.GetRequiredService<IParallelLogger>();
        ip.Should().BeSameAs( ia.ParallelLogger );
    }

    [Test]
    public void GrandOutput_MinimalFilter_configuration_works()
    {
        DemoSinkHandler.Reset();
        var config = new DynamicConfigurationSource();
        config["CK-Monitoring:GrandOutput:Handlers:CK.Monitoring.Hosting.Tests.DemoSinkHandler, CK.Monitoring.Hosting.Tests"] = "true";

        var host = new HostBuilder()
                    .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                    .UseCKMonitoring()
                    .Build();

        var m = new ActivityMonitor();
        m.ActualFilter.Should().Be( LogFilter.Undefined, "Initially Undefined." );

        config["CK-Monitoring:GrandOutput:MinimalFilter"] = "Debug";

        System.Threading.Thread.Sleep( 200 );
        m.ActualFilter.Should().Be( LogFilter.Debug, "First Debug applied." );

        config["CK-Monitoring:GrandOutput:MinimalFilter"] = "{Fatal,Debug}";
        System.Threading.Thread.Sleep( 200 );
        m.ActualFilter.Should().Be( new LogFilter( LogLevelFilter.Fatal, LogLevelFilter.Debug ), "Explicit {Off,Debug} filter." );

        config["CK-Monitoring:GrandOutput:MinimalFilter"] = null;
        System.Threading.Thread.Sleep( 200 );
        m.ActualFilter.Should().Be( new LogFilter( LogLevelFilter.Fatal, LogLevelFilter.Debug ), "Null doesn't change anything." );

        // Restores the Debug level (we are on the GrandOutput.Default).
        config["CK-Monitoring:GrandOutput:MinimalFilter"] = "Debug";
        System.Threading.Thread.Sleep( 200 );

        var texts = DemoSinkHandler.LogEvents.OrderBy( e => e.LogTime ).Select( e => e.Text ).ToArray();
        texts.Where( e => e != null && e.StartsWith( "GrandOutput.Default configuration n°4." ) ).Should().NotBeEmpty();
        texts.Where( e => e != null && e.StartsWith( "GrandOutput.Default configuration n°5." ) )
            .Should().BeEmpty( "There has been the initial configuration (n°0) and 4 reconfigurations." );
    }

    [Test]
    public async Task Invalid_configurations_are_skipped_and_errors_go_to_the_current_handlers_Async()
    {
        DemoSinkHandler.Reset();
        var config = new DynamicConfigurationSource();
        config["CK-Monitoring:GrandOutput:Handlers:CK.Monitoring.Hosting.Tests.DemoSinkHandler, CK.Monitoring.Hosting.Tests"] = "true";
        var host = new HostBuilder()
                    .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                    .UseCKMonitoring()
                    .Build();
        await host.StartAsync();

        var m = new ActivityMonitor( "The topic!" );

        m.Info( "BEFORE" );
        config["CK-Monitoring:GrandOutput:Handlers:Invalid Handler"] = "true";
        m.Info( "AFTER" );

        await host.StopAsync();

        DemoSinkHandler.LogEvents.Select( e => e.Text ).Should()
               .Contain( "Topic: The topic!" )
               .And.Contain( "BEFORE" )
               .And.Contain( "While applying dynamic configuration." )
               .And.Contain( "AFTER" );
    }


    [Test]
    public async Task Configuration_changes_dont_stutter_Async()
    {
        DemoSinkHandler.Reset();
        var config = new DynamicConfigurationSource();
        config["CK-Monitoring:GrandOutput:Handlers:CK.Monitoring.Hosting.Tests.DemoSinkHandler, CK.Monitoring.Hosting.Tests"] = "true";
        var host = new HostBuilder()
                    .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                    .UseCKMonitoring()
                    .Build();
        await host.StartAsync();

        var m = new ActivityMonitor( "The starting topic!" );

        config["CK-Monitoring:GrandOutput:Handlers:Console"] = "true";

        await Task.Delay( 200 );

        m.Info( "DONE!" );

        await host.StopAsync();

        var texts = DemoSinkHandler.LogEvents.OrderBy( e => e.LogTime ).Select( e => e.Text ).Concatenate( System.Environment.NewLine );
        texts.Should()
               .Contain( "GrandOutput.Default configuration n°0" )
               .And.Contain( "GrandOutput.Default configuration n°1" )
               .And.NotContain( "GrandOutput.Default configuration n°2" )
               .And.Contain( "DONE!" );
    }


    [Test]
    public async Task finding_MailAlerter_handler_by_conventions_Async()
    {
        // Define tag since this assembly doesn't depend on CK.Monitoring.MailAlerterHandler.
        var sendMailTag = ActivityMonitor.Tags.Register( "SendMail" );
        // Copy the assembly.
        var runningDir = new NormalizedPath( AppContext.BaseDirectory );
        var target = runningDir.AppendPart( "CK.Monitoring.MailAlerterHandler.dll" );
        var source = new NormalizedPath( AppContext.BaseDirectory.Replace( "CK.Monitoring.Hosting.Tests", "CK.Monitoring.MailAlerterHandler" ) )
                            .AppendPart( "CK.Monitoring.MailAlerterHandler.dll" );

        if( !File.Exists( target ) )
        {
            File.Copy( source, target, overwrite: true );
        }

        var config = new DynamicConfigurationSource();
        config["CK-Monitoring:GrandOutput:Handlers:MailAlerter:Email"] = "test@test.com";
        var host = new HostBuilder()
                    .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                    .UseCKMonitoring()
                    .ConfigureServices( services =>
                    {
                        var m = new ActivityMonitor();
                        m.Info( sendMailTag, "Hello World!" );
                    } )
                    .Build();
        await host.StartAsync();
        await host.StopAsync();

        // The assembly has been loaded.
        var a = AppDomain.CurrentDomain.GetAssemblies().Single( a => a.GetName().Name == "CK.Monitoring.MailAlerterHandler" );
        var t = a.GetType( "CK.Monitoring.Handlers.MailAlerter" );
        Throw.DebugAssert( t != null );
        var sent = (string?)t.GetField( "LastMailSent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static )!.GetValue( null );
        sent.Should().Be( "Hello World!" );
    }

    [Test]
    public async Task StaticGates_works_Async()
    {
        AsyncLock.Gate.IsOpen.Should().BeFalse();
        AsyncLock.Gate.HasDisplayName.Should().BeTrue( "Otherwise it wouldn't be configurable." );
        AsyncLock.Gate.DisplayName.Should().Be( "AsyncLock" );
        try
        {
            var config = new DynamicConfigurationSource();
            config["CK-Monitoring:GrandOutput:StaticGates"] = "AsyncLock";
            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                        .UseCKMonitoring()
                        .Build();
            await host.StartAsync();

            AsyncLock.Gate.IsOpen.Should().BeTrue();

            await host.StopAsync();
        }
        finally
        {
            AsyncLock.Gate.IsOpen = false;
        }
    }

    [Test]
    public async Task DotNetEventSources_works_Async()
    {
        var current = DotNetEventSourceCollector.GetLevel( "System.Runtime", out var found );
        found.Should().BeTrue();
        current.Should().BeNull();
        try
        {
            var config = new DynamicConfigurationSource();
            config["CK-Monitoring:GrandOutput:DotNetEventSources"] = "System.Runtime:V(erbose only V matters)";
            var host = new HostBuilder()
                        .ConfigureAppConfiguration( ( hostingContext, c ) => c.Add( config ) )
                        .UseCKMonitoring()
                        .Build();
            await host.StartAsync();

            var configured = DotNetEventSourceCollector.GetLevel( "System.Runtime", out _ );
            configured.Should().Be( System.Diagnostics.Tracing.EventLevel.Verbose );
            await host.StopAsync();
        }
        finally
        {
            DotNetEventSourceCollector.Disable( "System.Runtime" );
        }
    }

}
