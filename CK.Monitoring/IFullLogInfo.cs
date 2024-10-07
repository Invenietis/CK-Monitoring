using System;
using CK.Core;

namespace CK.Monitoring;

/// <summary>
/// Information added to a <see cref="ILogEntry"/> by <see cref="IFullLogEntry"/>.
/// </summary>
public interface IFullLogInfo
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
    /// Gets the previous entry type. <see cref="LogEntryType.None"/> when unknown.
    /// </summary>
    LogEntryType PreviousEntryType { get; }

    /// <summary>
    /// Gets the previous log time. <see cref="DateTimeStamp.Unknown"/> when unknown.
    /// </summary>
    DateTimeStamp PreviousLogTime { get; }
}
