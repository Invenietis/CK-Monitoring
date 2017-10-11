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
        /// Clones this configuration object.
        /// Since this object has no state, we currently return this object.
        /// </summary>
        /// <returns>This object (since this object is stateless).</returns>
        public IHandlerConfiguration Clone()
        {
            return this;
        }
    }
}
