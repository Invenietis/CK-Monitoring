using System;
using System.IO;
using System.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class CriticalErrorsTests
    {
        [SetUp]
        public void InitializePath() => TestHelper.InitalizePaths();

        // We cannot test since the test process fails fast.
        [Explicit]
        [TestCase( "Trace.Fail" )]
        [TestCase( "Trace.Assert" )]
        [TestCase( "Debug.Fail" )]
        [TestCase( "Debug.Assert" )]
        public void Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener( string action )
        {
            Assume.That( ExplicitTestManager.IsExplicitAllowed, "Press Ctrl key to run this test." );
            NormalizedPath folder = LogFile.RootLogPath + nameof( Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener );
            Directory.CreateDirectory( folder );
            var textConf = new Handlers.TextFileConfiguration() { Path = nameof( Debug_and_Trace_FailFast_are_handled_by_the_MonitorTraceListener ) };
            using( GrandOutput g = new GrandOutput( new GrandOutputConfiguration().AddHandler( textConf ) ) )
            {
                // This is what the GrandOtput.Default does.
                System.Diagnostics.Trace.Listeners.Clear();
                System.Diagnostics.Trace.Listeners.Add( new MonitorTraceListener( g, true ) );
                switch( action )
                {
                    case "Trace.Fail": System.Diagnostics.Trace.Fail( $"FailFast! {action}" ); break;
                    case "Trace.Assert": System.Diagnostics.Trace.Assert( false, $"FailFast! {action}" ); break;
                    case "Debug.Fail": System.Diagnostics.Debug.Fail( $"FailFast! {action}" ); break;
                    case "Debug.Assert": System.Diagnostics.Debug.Assert( false, $"FailFast! {action}" ); break;
                }
            }
        }


    }
}
