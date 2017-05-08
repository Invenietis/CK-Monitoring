using System.Xml.Linq;
using CK.Core;
using System.IO;

namespace CK.Monitoring
{
    /// <summary>
    /// Configuration interface marker.
    /// Handlers must currently be in the same assembly and namespace as the configuration object
    /// and be named the same without the "Configuration" suffix.
    /// </summary>
    public interface IHandlerConfiguration
    {
    }
}
