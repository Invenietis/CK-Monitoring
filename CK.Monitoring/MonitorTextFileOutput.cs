using CK.Core;
using System.IO;

namespace CK.Monitoring
{
    /// <summary>
    /// Helper class that encapsulates temporary stream and final renaming for log entries streams.
    /// This currently handles only the maximum count of entries per file but this may be extended with options like "SubFolderMode" that can be based 
    /// on current time (to group logs inside timed intermediate folders like one per day: 2014/01/12 or 2014-01/12, etc.). 
    /// </summary>
    public class MonitorTextFileOutput : MonitorFileOutputBase
    {
        readonly MulticastLogEntryTextBuilder _builder;
        StreamWriter _writer;

        /// <summary>
        /// Initializes a new file for <see cref="IMulticastLogEntry"/>: the final file name is based on <see cref="FileUtil.FileNameUniqueTimeUtcFormat"/> with a ".ckmon" extension.
        /// You must call <see cref="MonitorFileOutputBase.Initialize">Initialize</see> before actually using this object.
        /// </summary>
        /// <param name="configuredPath">The path: it can be absolute and when relative, it will be under <see cref="SystemActivityMonitor.RootLogPath"/> (that must be set).</param>
        /// <param name="maxCountPerFile">Maximum number of entries per file. Must be greater than 1.</param>
        /// <param name="useGzipCompression">True to gzip the file.</param>
        public MonitorTextFileOutput( string configuredPath, int maxCountPerFile, bool useGzipCompression )
            : base( configuredPath, ".txt" + (useGzipCompression ? ".gzip" : string.Empty), maxCountPerFile, useGzipCompression )
        {
            _builder = new MulticastLogEntryTextBuilder();
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="e">The log entry.</param>
        public void Write( IMulticastLogEntry e )
        {
            BeforeWriteEntry();
            _builder.AppendEntry( e );
            _writer.Write( _builder.Builder.ToString() );
            AfterWriteEntry();
            _builder.Builder.Clear();
        }

        /// <summary>
        /// Called when a new file is created.
        /// </summary>
        /// <returns>The created stream.</returns>
        protected override Stream OpenNewFile()
        {
            Stream s = base.OpenNewFile();
            _writer = new StreamWriter( s );
            _builder.Reset();
            return s;
        }

        /// <summary>
        /// Called when the current file is closed.
        /// </summary>
        protected override void CloseCurrentFile()
        {
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
            base.CloseCurrentFile();
        }
    }
}
