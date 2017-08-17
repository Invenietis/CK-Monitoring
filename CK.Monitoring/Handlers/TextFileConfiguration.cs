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
        /// Clones this configuration.
        /// </summary>
        /// <returns>Clone of this configuration.</returns>
        public override IHandlerConfiguration Clone()
        {
            return new TextFileConfiguration()
            {
                Path = Path,
                MaxCountPerFile = MaxCountPerFile,
                AutoFlushRate = AutoFlushRate
            };
        }
    }
}
