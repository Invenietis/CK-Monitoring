using System;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Configuration object for <see cref="BinaryFile"/>.
    /// </summary>
    public class BinaryFileConfiguration : FileConfigurationBase
    {
        /// <summary>
        /// Gets or sets whether to use Gzip compression after closing log files.
        /// Defaults to false.
        /// </summary>
        public bool UseGzipCompression { get; set; }

        /// <summary>
        /// Gets or sets the rate of file housekeeping tasks execution (automatic log file deletion).
        /// This is a multiple of <see cref="GrandOutputConfiguration.TimerDuration"/>,
        /// and defaults to 1800 (which is 15 minutes with the default <see cref="GrandOutputConfiguration.TimerDuration"/> of 500ms).
        /// Setting this to zero disables housekeeping entirely.
        /// </summary>
        public int HousekeepingRate { get; set; } = 1800;

        /// <summary>
        /// Gets or sets the minimum number of days to keep log files, when housekeeping is enabled via <see cref="HousekeepingRate"/>.
        /// Log files older than this will be deleted.
        /// Setting this to <see cref="TimeSpan.Zero"/> disables automatic file deletion.
        /// </summary>
        public TimeSpan MinimumTimeSpanToKeep { get; set; } = TimeSpan.FromDays( 60 );

        /// <summary>
        /// Gets or sets the number of days in <see cref="MinimumTimeSpanToKeep"/> as an integer.
        /// If a partial day is set in <see cref="MinimumTimeSpanToKeep"/>, the next positive integer is returned.
        /// </summary>
        public int MinimumDaysToKeep
        {
            get => Convert.ToInt32( Math.Ceiling( MinimumTimeSpanToKeep.TotalDays ) );
            set => MinimumTimeSpanToKeep = TimeSpan.FromDays( value );
        }

        /// <summary>
        /// Gets or sets the maximum total file size log files can use, in kilobytes.
        /// Defaults to 100 megabytes.
        /// Log files within <see cref="MinimumTimeSpanToKeep"/> or <see cref="MinimumDaysToKeep"/> will not be deleted,
        /// even if they exceed this value.
        /// </summary>
        public int MaximumTotalKbToKeep { get; set; } = 100_000;

        /// <summary>
        /// Clones this configuration.
        /// </summary>
        /// <returns>Clone of this configuration.</returns>
        public override IHandlerConfiguration Clone()
        {
            return new BinaryFileConfiguration()
            {
                Path = Path,
                MaxCountPerFile = MaxCountPerFile,
                UseGzipCompression = UseGzipCompression,
                HousekeepingRate = HousekeepingRate,
                MinimumTimeSpanToKeep = MinimumTimeSpanToKeep,
                MaximumTotalKbToKeep = MaximumTotalKbToKeep,
            };
        }
    }
}
