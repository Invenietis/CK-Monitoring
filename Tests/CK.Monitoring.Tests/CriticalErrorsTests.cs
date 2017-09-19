using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using System.Threading.Tasks;
using FluentAssertions;
using System.Threading;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class CriticalErrorsTests
    {
        [SetUp]
        public void InitializePath() => TestHelper.InitalizePaths();

        [Test]
        public void HandleCriticalErrors_quick_test()
        {
            string folder = TestHelper.PrepareLogFolder( "CriticalErrorsQuickTest" );

            var textConf = new Handlers.TextFileConfiguration() { Path = "CriticalErrorsQuickTest" };
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
