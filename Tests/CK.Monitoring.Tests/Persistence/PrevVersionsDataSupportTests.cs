using System.IO;
using System.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;

namespace CK.Monitoring.Tests.Persistence
{
    [TestFixture]
    public class PrevVersionsDataSupportTests
    {
        [SetUp]
        public void InitalizePaths() => TestHelper.InitalizePaths();

        [TestCase( 5 )]
        [TestCase( 6 )]
        public void reading_ckmon_files_in_previous_versions( int version )
        {
            var folder = Path.Combine( TestHelper.SolutionFolder, "Tests", "CK.Monitoring.Tests", "Persistence", "PrevVersionsData", "v" + version );
            var files = Directory.GetFiles( folder, "*.ckmon", SearchOption.TopDirectoryOnly );
            MultiLogReader reader = new MultiLogReader();
            bool newIndex;
            for( int i = 0; i < files.Length; ++i )
            {
                var f = reader.Add( files[i], out newIndex );
                newIndex.Should().BeTrue();
                f.Error.Should().BeNull();
                f.FileVersion.Should().Be( version );
            }
            var allEntries = reader.GetActivityMap().Monitors.SelectMany( m => m.ReadAllEntries().Select( e => e.Entry ) ).ToList();
            // v5 and v6 dit not have the Debug level.
            var allLevels = allEntries.Select( e => e.LogLevel & ~LogLevel.IsFiltered );
            allLevels.Should().NotContain( LogLevel.Debug )
                        .And.Contain( new[] { LogLevel.Trace, LogLevel.Info, LogLevel.Warn, LogLevel.Error, LogLevel.Fatal } );
        }

    }
}
