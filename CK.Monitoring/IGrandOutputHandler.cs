using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// Handler interface.
    /// </summary>
    public interface IGrandOutputHandler : IGrandOutputSink
    {
        /// <summary>
        /// Prepares the handler to receive events.
        /// This is called before any event will be received.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <returns>True on success, false on error (this handler will not be added).</returns>
        bool Activate(IActivityMonitor m);

        /// <summary>
        /// Called on a regular basis.
        /// Enables this handler to do any required housekeeping.
        /// </summary>
        /// <param name="timerSpan">Indicative timer duration.</param>
        void OnTimer( TimeSpan timerSpan );

        /// <summary>
        /// Attempts to apply configuration if possible.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="c">Configuration to apply.</param>
        /// <returns>True if the configuration applied.</returns>
        bool ApplyConfiguration(IActivityMonitor m, IHandlerConfiguration c);

        /// <summary>
        /// Closes this handler.
        /// This is called after the handler has been removed.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        void Deactivate(IActivityMonitor m);
    }

}
