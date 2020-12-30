using System;
using System.IO;
using System.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;
using CK.Text;

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

        [Test]
        public void HandleCriticalErrors_quick_test()
        {
            string folder = TestHelper.PrepareLogFolder( nameof( HandleCriticalErrors_quick_test ) );

            var textConf = new Handlers.TextFileConfiguration() { Path = nameof( HandleCriticalErrors_quick_test ) };
            var config = new GrandOutputConfiguration().AddHandler( textConf );
            using( GrandOutput g = new GrandOutput( config ) )
            {
                g.HandleCriticalErrors.Should().BeFalse();
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "NOSHOW" ), null );
                ActivityMonitor.CriticalErrorCollector.WaitOnErrorFromBackgroundThreadsPending();
                g.HandleCriticalErrors = true;
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "SHOW 1" ), null );
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "SHOW 2" ), "...with comment..." );
                ActivityMonitor.CriticalErrorCollector.WaitOnErrorFromBackgroundThreadsPending();
                g.HandleCriticalErrors = false;
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "NOSHOW" ), null );
                ActivityMonitor.CriticalErrorCollector.WaitOnErrorFromBackgroundThreadsPending();
            }
            string textLogged = File.ReadAllText( Directory.EnumerateFiles( folder ).Single() );
            textLogged.Should()
                        .Contain( "SHOW 1" )
                        .And.Contain( "SHOW 2" )
                        .And.Contain( "...with comment..." )
                        .And.NotContain( "NOSHOW" );
        }

        public class BuggySinkConfiguration : IHandlerConfiguration
        {
            public IHandlerConfiguration Clone() => new BuggySinkConfiguration();
        }

        public class BuggySinkHandler : IGrandOutputHandler
        {
            public BuggySinkHandler( BuggySinkConfiguration conf )
            {
            }

            public bool Activate( IActivityMonitor m ) => true;

            public bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c )
            {
                return c is BuggySinkConfiguration;
            }

            public void Deactivate( IActivityMonitor m )
            {
            }

            public void Handle( IActivityMonitor m, GrandOutputEventInfo logEvent )
            {
                throw new Exception( "From inside BuggySinkHandler" );
            }

            public void OnTimer( IActivityMonitor m, TimeSpan timerSpan )
            {
            }
        }

        public class KExistePasConfiguration : IHandlerConfiguration
        {
            public IHandlerConfiguration Clone() => new KExistePasConfiguration();
        }

        [Test]
        public void CriticalErrors_handle_sink_creation_error()
        {
            string folder = TestHelper.PrepareLogFolder( "CriticalErrorsSinkCreationError" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "CriticalErrorsSinkCreationError" };
            var config = new GrandOutputConfiguration()
                                .AddHandler( textConf )
                                .AddHandler( new KExistePasConfiguration() );
            using( GrandOutput g = new GrandOutput( config, true ) )
            {
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "SHOW 1" ), null );
                ActivityMonitor.CriticalErrorCollector.WaitOnErrorFromBackgroundThreadsPending();
                ActivityMonitor.CriticalErrorCollector.Add( new Exception( "SHOW 2" ), "...with comment..." );
                ActivityMonitor.CriticalErrorCollector.WaitOnErrorFromBackgroundThreadsPending();
            }
            string textLogged = File.ReadAllText( Directory.EnumerateFiles( folder ).Single() );
            textLogged.Should()
                        .Contain( "SHOW 1" )
                        .And.Contain( "SHOW 2" )
                        .And.Contain( "...with comment..." )
                        .And.Contain( "While creating handler for CK.Monitoring.Tests.CriticalErrorsTests+KExistePasConfiguration." );
        }


    }
}
