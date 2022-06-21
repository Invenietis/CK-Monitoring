using System;
using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// Information required by a <see cref="IMulticastLogEntry"/>.
    /// </summary>
    public interface IMulticastLogInfo
    {
        /// <summary>
        /// Gets the GrandOutput identifier that emitted this log entry.
        /// This is the identifier of the dispatcher sink monitor.
        /// </summary>
        /// <remarks>
        /// This has been introduced in <see cref="LogReader.CurrentStreamVersion"/> 9.
        /// For log entries read before this version this is set to <see cref="GrandOutput.UnknownGrandOutputId"/>.
        /// </remarks>
        string GrandOutputId { get; }

        /// <summary>
        /// Gets the monitor identifier.
        /// This string is at least 4 characters long: either the special <see cref="GrandOutput.ExternalLogMonitorUniqueId"/>
        /// or a random base 64 url string.
        /// <para>
        /// The random identifier is currently 11 characters long.
        /// This may change but will never be longer than 64 characters.
        /// </para>
        /// </summary>
        string MonitorId { get; }

        /// <summary>
        /// Gets the depth of the entry in the source <see cref="MonitorId"/>.
        /// </summary>
        int GroupDepth { get; }

        /// <summary>
        /// Gets the previous entry type. <see cref="LogEntryType.None"/> when unknown.
        /// </summary>
        LogEntryType PreviousEntryType { get; }

        /// <summary>
        /// Gets the previous log time. <see cref="DateTimeStamp.Unknown"/> when unknown.
        /// </summary>
        DateTimeStamp PreviousLogTime { get; }
    }
}
