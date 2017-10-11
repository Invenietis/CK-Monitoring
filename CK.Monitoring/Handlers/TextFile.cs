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
        TextFileConfiguration _config;
        int _countFlush;

        /// <summary>
        /// Initializes a new <see cref="TextFile"/> based on a <see cref="TextFileConfiguration"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public TextFile( TextFileConfiguration config )
        {
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            _config = config;
            _file = new MonitorTextFileOutput( config.Path, config.MaxCountPerFile, false );
            _countFlush = _config.AutoFlushRate;
        }

        /// <summary>
        /// Initialization of the handler: computes the path.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
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
        /// <param name="m">The monitor to use.</param>
        /// <param name="logEvent">The log entry.</param>
        public void Handle( IActivityMonitor m, GrandOutputEventInfo logEvent )
        {
            _file.Write( logEvent.Entry );
        }

        /// <summary>
        /// Automatically flushes the file based on <see cref="TextFileConfiguration.AutoFlushRate"/>.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="timerSpan">Indicative timer duration.</param>
        public void OnTimer( IActivityMonitor m, TimeSpan timerSpan )
        {
            // Don't really care of the overflow here.
            if( --_countFlush == 0 )
            {
                _file.Flush();
                _countFlush = _config.AutoFlushRate;
            }
        }

        /// <summary>
        /// Attempts to apply configuration if possible.
        /// The key is the <see cref="FileConfigurationBase.Path"/>: the <paramref name="c"/>
        /// must be a <see cref="TextFileConfiguration"/> with the exact same path
        /// for this reconfiguration to be applied.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="c">Configuration to apply.</param>
        /// <returns>True if the configuration applied.</returns>
        public bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c )
        {
            TextFileConfiguration cF = c as TextFileConfiguration;
            if( cF == null || cF.Path != _config.Path ) return false;
            _config = cF;
            _file.MaxCountPerFile = cF.MaxCountPerFile;
            _file.Flush();
            _countFlush = _config.AutoFlushRate;
            return true;
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
