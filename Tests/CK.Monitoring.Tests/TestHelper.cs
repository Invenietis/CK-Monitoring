using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CK.Core;
using NUnit.Framework;

namespace CK.Monitoring
{
    static class TestHelper
    {
        static NormalizedPath _solutionFolder;

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

        public static NormalizedPath SolutionFolder
        {
            get
            {
                if( _solutionFolder == null ) InitalizePaths();
                return _solutionFolder;
            }
        }

        public static void WaitForNoMoreAliveInputLogEntry()
        {
            int count = 0;
            while( InputLogEntry.AliveCount > 0 )
            {
                Thread.Sleep( 50 );
                if( ++count > 20 ) Throw.InvalidOperationException( $"InputLogEntry.AliveCount stays at {InputLogEntry.AliveCount} after 1 second." );
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

        /// <summary>
        /// Uses FileShare.ReadWrite: this cannot be replaced by the simple File.ReadAllText method.
        /// </summary>
        /// <param name="path">Path to read.</param>
        /// <returns>Text content.</returns>
        public static string FileReadAllText( string path )
        {
            using( Stream s = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan ) )
            using( StreamReader r = new StreamReader( s ) )
            {
                return r.ReadToEnd();
            }
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
            using( m?.OpenTrace( $"Reading files from '{directory.FullName}' {(recurse ? "(recursive)" : null)}." ) )
            {
                var files = reader.Add( directory.EnumerateFiles( "*.ckmon", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly ).Select( f => f.FullName ) );
                if( files.Count == 0 )
                {
                    if( m != null ) m.Warn( "No *.ckmon files found!" );
                }
                else
                {
                    var monitors = reader.CreateActivityMap().Monitors;
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
                            m?.Info( $"Skipping activity from '{mon.MonitorId}'." );
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
            if( _solutionFolder.IsEmptyPath )
            {
                NormalizedPath path = AppContext.BaseDirectory;
                Debug.Assert( path.Parts[^3] == "bin" );
                var root = path.PathsToFirstPart( null, new[] { "CK-Monitoring.sln" } ).FirstOrDefault( p => File.Exists( p ) );
                if( root.IsEmptyPath ) Throw.InvalidOperationException( $"Unable to find CK-Monitoring.sln above '{AppContext.BaseDirectory}'." );
                _solutionFolder = root.RemoveLastPart();
                LogFile.RootLogPath = path.RemoveLastPart( 3 ).AppendPart( "Logs" );
                ConsoleMonitor.Info( $"SolutionFolder is: {_solutionFolder}\r\nRootLogPath is: {LogFile.RootLogPath}" );
            }
        }

    }
}
