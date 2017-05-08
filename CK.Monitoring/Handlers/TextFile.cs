using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Text file handler.
    /// </summary>
    public sealed class TextFile : IGrandOutputHandler
    {
        readonly MonitorTextFileOutput _file;

        /// <summary>
        /// Initializes a new <see cref="TextFile"/> based on a <see cref="TextFileConfiguration"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public TextFile( TextFileConfiguration config )
        {
            if( config == null ) throw new ArgumentNullException( nameof(config) );
            _file = new MonitorTextFileOutput( config.Path, config.MaxCountPerFile, false );
        }

        /// <summary>
        /// Initialization of the handler: computes the path.
        /// </summary>
        /// <param name="m"></param>
        public bool Activate( IActivityMonitor m )
        {
            using( m.OpenGroup( LogLevel.Trace, $"Initializing TextFile handler (MaxCountPerFile = {_file.MaxCountPerFile}).", null ) )
            {
                return _file.Initialize( m );
            }
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="logEvent">The log entry.</param>
        public void Handle( GrandOutputEventInfo logEvent )
        {
            _file.Write( logEvent.Entry );
        }

        /// <summary>
        /// Does nothing since files are automatically managed (relies on <see cref="FileConfigurationBase.MaxCountPerFile"/>).
        /// </summary>
        /// <param name="timerSpan">Indicative timer duration.</param>
        public void OnTimer(TimeSpan timerSpan)
        {
        }

        /// <summary>
        /// Closes the file if it is opened.
        /// </summary>
        /// <param name="m">The monitor to use to track activity.</param>
        public void Deactivate( IActivityMonitor m )
        {
            m.SendLine( LogLevel.Info, $"Closing file for TextFile handler.", null );
            _file.Close();
        }

    }

}
