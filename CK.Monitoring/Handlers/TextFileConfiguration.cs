using System;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Configuration object for <see cref="TextFile"/>.
    /// </summary>
    public class TextFileConfiguration : FileConfigurationBase
    {
        /// <summary>
        /// Gets or sets the rate of the auto flush to be able to read
        /// the temporary currrent file content.
        /// This is a multiple of <see cref="GrandOutputConfiguration.TimerDuration"/>
        /// and defaults to 6 (default GrandOutputConfiguration timer duration being 500 milliseconds, this
        /// flushes the text approximately every 3 seconds).
        /// Setting this to zero disables the timed-base flush.
        /// </summary>
        public int AutoFlushRate { get; set; } = 6;

        /// <summary>
        /// Gets or sets the rate of file housekeeping tasks execution (automatic log file deletion).
        /// This is a multiple of <see cref="GrandOutputConfiguration.TimerDuration"/>,
        /// and defaults to 240 (which is 2 minutes with the default <see cref="GrandOutputConfiguration.TimerDuration"/> of 500ms).
        /// Setting this to zero disables housekeeping entirely.
        /// </summary>
        public int HousekeepingRate { get; set; } = 240;

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
        /// Log files within <see cref="MinimumTimeSpanToKeep"/> or <see cref="MinimumDaysToKeep"/> will not be deleted,
        /// even if they exceed this value.
        /// </summary>
        public int MaximumTotalKbToKeep { get; set; } = 5000;

        /// <summary>
        /// Clones this configuration.
        /// </summary>
        /// <returns>Clone of this configuration.</returns>
        public override IHandlerConfiguration Clone()
        {
            return new TextFileConfiguration()
            {
                Path = Path,
                MaxCountPerFile = MaxCountPerFile,
                AutoFlushRate = AutoFlushRate,
                HousekeepingRate = HousekeepingRate,
                MinimumTimeSpanToKeep = MinimumTimeSpanToKeep,
                MaximumTotalKbToKeep = MaximumTotalKbToKeep,
            };
        }
    }
}
