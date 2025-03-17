using NUnit.Framework;
using CK.Core;
using Shouldly;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace CK.Monitoring.Tests;

[TestFixture]
public class CKMonWriterClientTests
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

    [Test]
    public void testing_CKMonWriterClient_output()
    {
        Debug.Assert( LogFile.RootLogPath != null );
        var path = Path.Combine( LogFile.RootLogPath, "CKMonWriterClient" );
        if( Directory.Exists( path ) ) Directory.Delete( path, true );
        var m = new ActivityMonitor( ActivityMonitorOptions.SkipAutoConfiguration );
        // This test works only because CKMonWriterClient does not talk to
        // the InternalMonitor: there is exactly 3 traces, so only one file
        // is created and closed (on the theirs log).
        var client = m.Output.RegisterClient( new CKMonWriterClient( "CKMonWriterClient", 3 ) );
        client.IsOpened.ShouldBeTrue();
        m.Info( "Info n°1." );
        m.Info( "Info n°2." );
        m.Info( "Info n°3." );

        Directory.EnumerateFiles( path ).Count().ShouldBe( 1 );

    }
}
