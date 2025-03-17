using System.IO;
using System.Linq;
using CK.Core;
using NUnit.Framework;
using Shouldly;

namespace CK.Monitoring.Tests.Persistence;

[TestFixture]
public class PrevVersionsDataSupportTests
{
    [SetUp]
    public void InitalizePaths()
    {
        TestHelper.InitalizePaths();
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    [TearDown]
    public void WaitForNoMoreAliveInputLogEntry()
    {
        TestHelper.WaitForNoMoreAliveInputLogEntry();
    }

    [TestCase( 5 )]
    [TestCase( 6 )]
    [TestCase( 7 )]
    [TestCase( 8 )]
    public void reading_ckmon_files_in_previous_versions( int version )
    {
        var folder = Path.Combine( TestHelper.SolutionFolder, "Tests", "CK.Monitoring.Tests", "Persistence", "PrevVersionsData", "v" + version );
        var files = Directory.GetFiles( folder, "*.ckmon", SearchOption.TopDirectoryOnly );
        using MultiLogReader reader = new MultiLogReader();
        bool newIndex;
        for( int i = 0; i < files.Length; ++i )
        {
            var f = reader.Add( files[i], out newIndex );
            newIndex.ShouldBeTrue();
            f.Error.ShouldBeNull();
            f.FileVersion.ShouldBe( version );
        }
        var allEntries = reader.GetActivityMap().Monitors.SelectMany( m => m.ReadAllEntries().Select( e => e.Entry ) ).ToList();
        // v5 and v6 did not have the Debug level.
        if( version <= 6 )
        {
            var allLevels = allEntries.Select( e => e.LogLevel & ~LogLevel.IsFiltered );
            allLevels.ShouldNotContain( LogLevel.Debug )
                     .ShouldContain( LogLevel.Trace )
                     .ShouldContain( LogLevel.Info )
                     .ShouldContain( LogLevel.Warn )
                     .ShouldContain( LogLevel.Error )
                     .ShouldContain( LogLevel.Fatal );
        }
    }
}
