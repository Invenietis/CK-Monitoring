using System;
using System.IO;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using System.Threading.Tasks;

namespace CK.Monitoring.Tests;

[TestFixture]
public class CriticalErrorsTests
{
    [SetUp]
    public void InitializePath()
    {
        TestHelper.InitalizePaths();
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    [TearDown]
    public void WaitForNoMoreAliveInputLogEntry()
    {
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    // We cannot test these... since the test process fails fast.
    [TestCase( "Trace.Fail", true, Explicit = true )]
    [TestCase( "Trace.Assert", true, Explicit = true )]
    [TestCase( "Debug.Fail", true, Explicit = true )]
    [TestCase( "Debug.Assert", true, Explicit = true )]
    // We can test these... since the test process DOES NOT fails fast.
    [TestCase( "Trace.Fail", false, Explicit = false )]
    [TestCase( "Trace.Assert", false, Explicit = false )]
    [TestCase( "Debug.Fail", false, Explicit = false )]
    [TestCase( "Debug.Assert", false, Explicit = false )]
    public async Task Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener_Async( string action, bool monitorTraceListenerFailFast )
    {
        NormalizedPath folder = LogFile.RootLogPath + nameof( Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener_Async );
        Directory.CreateDirectory( folder );
        var textConf = new Handlers.TextFileConfiguration() { Path = nameof( Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener_Async ) };

        GrandOutput g = new GrandOutput( new GrandOutputConfiguration().AddHandler( textConf ) );
        await using( g.ConfigureAwait( false ) )
        {
            // This is what the GrandOtput.Default does with its default options.
            System.Diagnostics.Trace.Listeners.Clear();
            System.Diagnostics.Trace.Listeners.Add( new MonitorTraceListener( g, monitorTraceListenerFailFast ) );
            try
            {
                switch( action )
                {
                    case "Trace.Fail": System.Diagnostics.Trace.Fail( $"FailFast! {action}" ); break;
                    case "Trace.Assert": System.Diagnostics.Trace.Assert( false, $"FailFast! {action}" ); break;
                    case "Debug.Fail": System.Diagnostics.Debug.Fail( $"FailFast! {action}" ); break;
                    case "Debug.Assert": System.Diagnostics.Debug.Assert( false, $"FailFast! {action}" ); break;
                }
            }
            catch( Exception ex )
            {
                monitorTraceListenerFailFast.ShouldBeFalse();
                ex.ShouldBeOfType<MonitoringFailFastException>();
            }
            monitorTraceListenerFailFast.ShouldBeFalse();
        }
    }


}
