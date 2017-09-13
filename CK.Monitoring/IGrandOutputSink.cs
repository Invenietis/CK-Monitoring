using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// Root log event handler interface.
    /// </summary>
    public interface IGrandOutputSink
    {
        /// <summary>
        /// Handles a log event.
        /// </summary>
        /// <param name="logEvent">The log event.</param>
        void Handle( GrandOutputEventInfo logEvent );
    }

}
