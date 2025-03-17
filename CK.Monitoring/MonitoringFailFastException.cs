using System;

namespace CK.Monitoring;

/// <summary>
/// Exception raised by <see cref="MonitorTraceListener"/> when <see cref="MonitorTraceListener.FailFast"/> is true
/// instead of calling <see cref="Environment.FailFast(string)"/>.
/// </summary>
public class MonitoringFailFastException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="MonitoringFailFastException"/>.
    /// </summary>
    /// <param name="message">The fail fast message.</param>
    public MonitoringFailFastException( string message )
        : base( message )
    {
    }
}
