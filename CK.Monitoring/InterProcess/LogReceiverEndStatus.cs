using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Monitoring.InterProcess
{
    /// <summary>
    /// Defines the final status of a <see cref=""/>
    /// </summary>
    public enum LogReceiverEndStatus
    {
        None,
        Normal,
        MissingEndMarker,
        Error
    }
}
