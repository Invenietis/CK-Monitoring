using System;
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
        int _countHousekeeping;

        /// <summary>
        /// Initializes a new <see cref="TextFile"/> based on a <see cref="TextFileConfiguration"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public TextFile( TextFileConfiguration config )
        {
            _config = config ?? throw new ArgumentNullException( nameof( config ) );
            _file = new MonitorTextFileOutput( config.Path, config.MaxCountPerFile, false );
            _countFlush = _config.AutoFlushRate;
            _countHousekeeping = _config.HousekeepingRate;
        }

        /// <summary>
        /// Initialization of the handler: computes the path.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        public bool Activate( IActivityMonitor m )
        {
            using( m.OpenTrace( $"Initializing TextFile handler (MaxCountPerFile = {_file.MaxCountPerFile})." ) )
            {
                return _file.Initialize( m );
            }
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="logEvent">The log entry.</param>
        public void Handle( IActivityMonitor m, IMulticastLogEntry logEvent )
        {
            _file.Write( logEvent );
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

            if( --_countHousekeeping == 0 )
            {
                _file.RunFileHousekeeping( m, _config.MinimumTimeSpanToKeep, _config.MaximumTotalKbToKeep * 1000L );
                _countHousekeeping = _config.HousekeepingRate;
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
            if( c is not TextFileConfiguration cF || cF.Path != _config.Path ) return false;
            _config = cF;
            _file.MaxCountPerFile = cF.MaxCountPerFile;
            _file.Flush();
            _countFlush = _config.AutoFlushRate;
            _countHousekeeping = _config.HousekeepingRate;
            return true;
        }

        /// <summary>
        /// Closes the file if it is opened.
        /// </summary>
        /// <param name="m">The monitor to use to track activity.</param>
        public void Deactivate( IActivityMonitor m )
        {
            m.Info( "Closing file for TextFile handler." );
            _file.Close();
        }

    }

}
