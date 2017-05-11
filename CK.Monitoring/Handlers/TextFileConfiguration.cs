using System.Xml.Linq;
using CK.Core;
using System.IO;

namespace CK.Monitoring.Handlers
{
    /// <summary>
    /// Configuration object for <see cref="TextFile"/>.
    /// </summary>
    public class TextFileConfiguration : FileConfigurationBase
    {
        /// <summary>
        /// Clones this configuration.
        /// </summary>
        /// <returns>Clone of this configuration.</returns>
        public override IHandlerConfiguration Clone()
        {
            return new TextFileConfiguration()
            {
                Path = Path,
                MaxCountPerFile = MaxCountPerFile
            };
        }
    }
}
