using CK.Core;
using System;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Binary file handler.
    /// </summary>
    public class BinaryFile : IGrandOutputHandler
    {
        MonitorBinaryFileOutput _file;
        BinaryFileConfiguration _config;
        int _countHousekeeping;
        private TimeSpan _minTimespan;
        int _maxKbToKeep;

        /// <summary>
        /// Initializes a new <see cref="BinaryFile"/> bound to its <see cref="BinaryFileConfiguration"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        public BinaryFile( BinaryFileConfiguration config )
        {
            if( config == null ) throw new ArgumentNullException( "config" );
            _file = new MonitorBinaryFileOutput( config.Path, config.MaxCountPerFile, config.UseGzipCompression );
            _config = config;
            _countHousekeeping = _config.HousekeepingRate;
            _minTimespan = _config.MinimumTimeSpanToKeep;
            _maxKbToKeep = _config.MaximumTotalKbToKeep;
        }

        /// <summary>
        /// Initialization of the handler: computes the path.
        /// </summary>
        /// <param name="m"></param>
        public bool Activate( IActivityMonitor m )
        {
            using( m.OpenGroup( LogLevel.Trace, $"Initializing BinaryFile handler (MaxCountPerFile = {_file.MaxCountPerFile}).", null ) )
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
        /// Does nothing since files are automatically managed (relies on <see cref="FileConfigurationBase.MaxCountPerFile"/>).
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="timerSpan">Indicative timer duration.</param>
        public void OnTimer( IActivityMonitor m, TimeSpan timerSpan )
        {
            if( --_countHousekeeping == 0 )
            {
                _file.RunFileHousekeeping( m, _config.MinimumTimeSpanToKeep, _config.MaximumTotalKbToKeep * 1000L );
                _countHousekeeping = _config.HousekeepingRate;
            }
        }

        /// <summary>
        /// Attempts to apply configuration if possible.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="c">Configuration to apply.</param>
        /// <returns>True if the configuration applied.</returns>
        public bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c )
        {
            BinaryFileConfiguration cF = c as BinaryFileConfiguration;
            if( cF == null || cF.Path != _config.Path ) return false;
            if( _config.UseGzipCompression != cF.UseGzipCompression )
            {
                _file.Close();
                _file = new MonitorBinaryFileOutput( _config.Path, _config.MaxCountPerFile, _config.UseGzipCompression );
            }
            else
            {
                _file.MaxCountPerFile = cF.MaxCountPerFile;
            }
            _config = cF;
            return true;
        }

        /// <summary>
        /// Closes the file if it is opened.
        /// </summary>
        /// <param name="m">The monitor to use to track activity.</param>
        public void Deactivate( IActivityMonitor m )
        {
            m.SendLine( LogLevel.Info, "Closing file for BinaryFile handler.", null );
            _file.Close();
        }

    }
}
