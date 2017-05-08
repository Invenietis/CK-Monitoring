using System;
using System.Collections.Generic;
using System.Text;

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
    }
}
