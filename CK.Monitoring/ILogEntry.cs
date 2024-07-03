using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// Unified interface for log entries with their <see cref="MonitorId"/> and <see cref="GroupDepth"/> whatever
    /// their <see cref="IBaseLogEntry.LogType"/> is.
    /// </summary>
    public interface ILogEntry : IBaseLogEntry
    {
        /// <summary>
        /// Gets the monitor identifier.
        /// This string is at least 4 characters long: either the special <see cref="ActivityMonitor.ExternalLogMonitorUniqueId"/>
        /// or a random base 64 url string.
        /// <para>
        /// The random identifier is currently 11 characters long.
        /// This may change but will never be longer than 64 characters.
        /// </para>
        /// </summary>
        string MonitorId { get; }

        /// <summary>
        /// Gets the depth of the entry in the source <see cref="MonitorId"/>.
        /// This is always available (whatever the <see cref="IBaseLogEntry.LogType">LogType</see> is <see cref="LogEntryType.OpenGroup"/>, <see cref="LogEntryType.CloseGroup"/>,
        /// or <see cref="LogEntryType.Line"/>).
        /// </summary>
        int GroupDepth { get; }

        /// <summary>
        /// Creates a base entry from this one.
        /// The <see cref="MonitorId"/> and <see cref="GroupDepth"/> are lost (but less memory is used).
        /// </summary>
        IBaseLogEntry CreateLightLogEntry();
    }
}
