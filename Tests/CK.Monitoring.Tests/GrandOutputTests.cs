using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CK.Core;
using NUnit.Framework;
using FluentAssertions;
using System.Threading;

namespace CK.Monitoring.Tests
{
    [TestFixture]
    public class GrandOutputTests
    {
        [SetUp]
        public void InitalizePaths() => TestHelper.InitalizePaths();

        [Test]
        public void artificially_generated_missing_log_entries_are_detected()
        {
            TestHelper.CleanupFolder(SystemActivityMonitor.RootLogPath + "MissingEntries");
            var emptyConfig = new GrandOutputConfiguration();
            var binaryConfig = new GrandOutputConfiguration().AddHandler(new Handlers.BinaryFileConfiguration() { Path = "MissingEntries" });

            using (GrandOutput g = new GrandOutput(emptyConfig))
            {
                var m = new ActivityMonitor(false);
                g.EnsureGrandOutputClient(m);
                m.Trace().Send("NoShow-1");
                g.ApplyConfiguration(emptyConfig);
                m.Trace().Send("NoShow-2");
                Thread.Sleep(300);
                g.ApplyConfiguration(binaryConfig);
                Thread.Sleep(300);
                m.Trace().Send("Show-1");
                Thread.Sleep(300);
                g.ApplyConfiguration(emptyConfig);
                Thread.Sleep(300);
                m.Trace().Send("NoShow-3");
            }
            var replayed = new ActivityMonitor(false);
            var c = replayed.Output.RegisterClient(new StupidStringClient());
            TestHelper.ReplayLogs(new DirectoryInfo(SystemActivityMonitor.RootLogPath + "MissingEntries"), true, mon => replayed, TestHelper.ConsoleMonitor);
            CollectionAssert.AreEqual(new[] { "<Missing log data>", "Show-1" }, c.Entries.Select(e => e.Text), StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void CKMon_binary_files_can_be_GZip_compressed()
        {
            string rootPath = SystemActivityMonitor.RootLogPath + @"Gzip";
            TestHelper.CleanupFolder( rootPath );

            var c = new GrandOutputConfiguration()
                            .AddHandler(new Handlers.BinaryFileConfiguration()
                            {
                                Path = rootPath + @"\OutputGzip",
                                UseGzipCompression = true
                            })
                            .AddHandler(new Handlers.BinaryFileConfiguration()
                            {
                                Path = rootPath + @"\OutputRaw",
                                UseGzipCompression = false
                            });

            using( GrandOutput g = new GrandOutput( c ) )
            {
                var taskA = Task.Factory.StartNew( () => DumpMonitorOutput( CreateMonitorAndRegisterGrandOutput( "Task A", g ) ), TaskCreationOptions.LongRunning);
                var taskB = Task.Factory.StartNew( () => DumpMonitorOutput( CreateMonitorAndRegisterGrandOutput( "Task B", g ) ), TaskCreationOptions.LongRunning);
                var taskC = Task.Factory.StartNew( () => DumpMonitorOutput( CreateMonitorAndRegisterGrandOutput( "Task C", g ) ), TaskCreationOptions.LongRunning);

                Task.WaitAll( taskA, taskB, taskC );
            }

            string[] gzipCkmons = TestHelper.WaitForCkmonFilesInDirectory( rootPath + @"\OutputGzip", 1 );
            string[] rawCkmons = TestHelper.WaitForCkmonFilesInDirectory( rootPath + @"\OutputRaw", 1 );

            gzipCkmons.Should().HaveCount( 1 );
            rawCkmons.Should().HaveCount( 1 );

            FileInfo gzipCkmonFile = new FileInfo( gzipCkmons.Single() );
            FileInfo rawCkmonFile = new FileInfo( rawCkmons.Single() );

            gzipCkmonFile.Exists.Should().BeTrue();
            rawCkmonFile.Exists.Should().BeTrue();

            // Test file size
            gzipCkmonFile.Length.Should().BeLessThan( rawCkmonFile.Length );

            // Test de-duplication between Gzip and non-Gzip
            MultiLogReader mlr = new MultiLogReader();
            var fileList = mlr.Add( new string[] { gzipCkmonFile.FullName, rawCkmonFile.FullName } );
            fileList.Should().HaveCount( 2 );

            var map = mlr.GetActivityMap();

            map.Monitors.Should().HaveCount( 3 );
            map.Monitors[0].ReadFirstPage(6000).Entries.Should().HaveCount(5410);
            map.Monitors[1].ReadFirstPage(6000).Entries.Should().HaveCount(5410);
            map.Monitors[2].ReadFirstPage(6000).Entries.Should().HaveCount(5410);
        }

        static IActivityMonitor CreateMonitorAndRegisterGrandOutput( string topic, GrandOutput go )
        {
            var m = new ActivityMonitor( applyAutoConfigurations:false, topic: topic );
            go.EnsureGrandOutputClient( m );
            return m;
        }

        static void DumpMonitorOutput( IActivityMonitor monitor )
        {
            Exception exception1;
            Exception exception2;

            try
            {
                throw new InvalidOperationException( "Exception!" );
            }
            catch( Exception e )
            {
                exception1 = e;
            }

            try
            {
                throw new InvalidOperationException( "Inception!", exception1 );
            }
            catch( Exception e )
            {
                exception2 = e;
            }
            const int nbLoop = 180;
            // Entry count:
            // 5 * (OpenTrace + Closer) = 10
            // 5 * nbLoop * 6 = 5400
            // => 5410
            // Since there is 3 parallel activities this fits into the 
            // default per file count of 20000.
            for (int i = 0; i < 5; i++)
            {
                using (monitor.OpenTrace().Send("Dump output loop {0}", i))
                {
                    for (int j = 0; j < nbLoop; j++)
                    {
                        monitor.Debug().Send("Debug log! {0}", j);
                        monitor.Trace().Send("Trace log! {0}", j);
                        monitor.Info().Send("Info log! {0}", j);
                        monitor.Warn().Send("Warn log! {0}", j);
                        monitor.Error().Send("Error log! {0}", j);
                        monitor.Error().Send("Fatal log! {0}", j);
                        monitor.Error().Send(exception2, "Exception log! {0}", j);
                    }
                }
            }
        }

    }
}
