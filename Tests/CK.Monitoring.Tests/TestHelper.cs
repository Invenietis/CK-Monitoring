using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CK.Core;
using NUnit.Framework;
using CK.Text;

namespace CK.Monitoring.Tests
{
    static class TestHelper
    {
        static string _solutionFolder;

        static readonly IActivityMonitor _monitor;
        static readonly ActivityMonitorConsoleClient _console;

        static TestHelper()
        {
            _monitor = new ActivityMonitor();
            // Do not pollute the console by default...
            // ... but this may be useful sometimes: LogsToConsole does the job.
            _console = new ActivityMonitorConsoleClient();
        }

        public static IActivityMonitor ConsoleMonitor => _monitor;

        public static bool LogsToConsole
        {
            get { return _monitor.Output.Clients.Contains( _console ); }
            set
            {
                if( value ) _monitor.Output.RegisterUniqueClient( c => c == _console, () => _console );
                else _monitor.Output.UnregisterClient( _console );
            }
        }

        public static string FileReadAllText( string path )
        {
            using( Stream s = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan ) )
            using( StreamReader r = new StreamReader( s ) )
            {
                return r.ReadToEnd();
            }
        }

        public static string SolutionFolder
        {
            get
            {
                if( _solutionFolder == null ) InitalizePaths();
                return _solutionFolder;
            }
        }

        public static string CriticalErrorsFolder
        {
            get
            {
                if( _solutionFolder == null ) InitalizePaths();
                return LogFile.RootLogPath + "CriticalErrors";
            }
        }

        public static List<StupidStringClient> ReadAllLogs( DirectoryInfo folder, bool recurse )
        {
            List<StupidStringClient> logs = new List<StupidStringClient>();
            ReplayLogs( folder, recurse, mon =>
            {
                var m = new ActivityMonitor( false );
                logs.Add( m.Output.RegisterClient( new StupidStringClient() ) );
                return m;
            }, TestHelper.ConsoleMonitor );
            return logs;
        }

        public static string[] WaitForCkmonFilesInDirectory( string directoryPath, int minFileCount )
        {
            string[] files;
            for( ;;)
            {
                files = Directory.GetFiles( directoryPath, "*.ckmon", SearchOption.TopDirectoryOnly );
                if( files.Length >= minFileCount ) break;
                Thread.Sleep( 200 );
            }
            foreach( var f in files )
            {
                if( !FileUtil.CheckForWriteAccess( f, 3000 ) )
                {
                    throw new CKException( "CheckForWriteAccess exceeds 3000 milliseconds..." );
                }
            }
            return files;
        }

        public static void ReplayLogs( DirectoryInfo directory, bool recurse, Func<MultiLogReader.Monitor, ActivityMonitor> monitorProvider, IActivityMonitor m = null )
        {
            var reader = new MultiLogReader();
            using( m != null ? m.OpenTrace( $"Reading files from '{directory.FullName}' {(recurse ? "(recursive)" : null)}." ) : null )
            {
                var files = reader.Add( directory.EnumerateFiles( "*.ckmon", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly ).Select( f => f.FullName ) );
                if( files.Count == 0 )
                {
                    if( m != null ) m.Warn( "No *.ckmon files found!" );
                }
                else
                {
                    var monitors = reader.GetActivityMap().Monitors;
                    if( m != null )
                    {
                        m.Trace( String.Join( Environment.NewLine, files ) );
                        m.CloseGroup( $"Found {files.Count} file(s) containing {monitors.Count} monitor(s)." );
                        m.OpenTrace( "Extracting entries." );
                    }
                    foreach( var mon in monitors )
                    {
                        var replay = monitorProvider( mon );
                        if( replay == null )
                        {
                            if( m != null ) m.Info( $"Skipping activity from '{mon.MonitorId}'." );
                        }
                        else
                        {
                            mon.Replay( replay, m );
                        }
                    }
                }
            }
        }

        public static string PrepareLogFolder( string subfolder )
        {
            if( _solutionFolder == null ) InitalizePaths();
            string p = LogFile.RootLogPath + subfolder;
            CleanupFolder( p );
            return p;
        }

        static void CleanupFolder( string folder )
        {
            if( _solutionFolder == null ) InitalizePaths();
            int tryCount = 0;
            for( ;;)
            {
                try
                {
                    while( Directory.Exists( folder ) ) Directory.Delete( folder, true );
                    Directory.CreateDirectory( folder );
                    File.WriteAllText( Path.Combine( folder, "TestWrite.txt" ), "Test write works." );
                    File.Delete( Path.Combine( folder, "TestWrite.txt" ) );
                    return;
                }
                catch( Exception ex )
                {
                    if( ++tryCount == 20 ) throw;
                    ConsoleMonitor.Info( "While cleaning up test directory. Retrying.", ex );
                    Thread.Sleep( 100 );
                }
            }
        }

        static public void InitalizePaths()
        {
            if( _solutionFolder == null )
            {
                NormalizedPath path = AppContext.BaseDirectory;
                var s = path.PathsToFirstPart( null, new[] { "CK-Monitoring.sln" } ).FirstOrDefault( p => File.Exists( p ) );
                if( s.IsEmptyPath ) throw new InvalidOperationException( $"Unable to find CK-Monitoring.sln above '{AppContext.BaseDirectory}'." );
                _solutionFolder = s.RemoveLastPart();
                LogFile.RootLogPath = Path.Combine( _solutionFolder, "Tests", "CK.Monitoring.Tests", "Logs" );
                ConsoleMonitor.Info( $"SolutionFolder is: {_solutionFolder}\r\nRootLogPath is: {LogFile.RootLogPath}" );
            }
            Assert.That( Directory.Exists( CriticalErrorsFolder ) );
        }

    }
}
