using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Configuration object for <see cref="Console"/> handler.
    /// There is currently no option for this configuration. Logs are written to the console
    /// using the <see cref="MulticastLogEntryTextBuilder"/> (just
    /// like <see cref="TextFileConfiguration"/> does).
    /// </summary>
    public class ConsoleConfiguration : IHandlerConfiguration
    {
        /// <summary>
        /// Gets or sets the Background color used to log the lines.
        /// </summary>
        public ConsoleColor BackgroundColor { get; set; }

        /// <summary>
        /// Time format string used to display the DateTime before each logged line.
        /// If not set, the format used will be "yyyy-MM-dd HH\hmm.ss.fff"
        /// </summary>
        public string DateFormat { get; set; }

        /// <summary>
        /// When set to false, the log times are displayed with the +delta seconds from its minute: the full time appears
        /// only once per minute.
        /// </summary>
        public bool UseDeltaTime { get; set; }

        /// <summary>
        /// Clones this configuration object.
        /// Since this object has no state, we currently return this object.
        /// </summary>
        /// <returns>This object (since this object is stateless).</returns>
        public IHandlerConfiguration Clone()
        {
            return new ConsoleConfiguration
            {
                BackgroundColor = BackgroundColor,
                DateFormat = DateFormat,
                UseDeltaTime = UseDeltaTime
            };
        }
    }
}
