
namespace CK.Monitoring
{

    /// <summary>
    /// Type of a <see cref="IBaseLogEntry"/>.
    /// </summary>
    public enum LogEntryType : byte
    {
        /// <summary>
        /// Non applicable.
        /// </summary>
        None = 0,

        /// <summary>
        /// A standard log entry.
        /// Except <see cref="IBaseLogEntry.Conclusions"/> (reserved to <see cref="CloseGroup"/>), all other properties of the <see cref="IBaseLogEntry"/> may be set.
        /// </summary>
        Line = 1,

        /// <summary>
        /// Group is opened.
        /// Except <see cref="IBaseLogEntry.Conclusions"/> (reserved to <see cref="CloseGroup"/>), all other properties of the <see cref="IBaseLogEntry"/> may be set.
        /// </summary>
        OpenGroup = 2,

        /// <summary>
        /// Group is closed. 
        /// Note that the only available information are <see cref="IBaseLogEntry.Conclusions"/>, <see cref="IBaseLogEntry.LogLevel"/> and <see cref="IBaseLogEntry.LogTime"/>.
        /// All other properties are set to their default: <see cref="IBaseLogEntry.Text"/> for instance is null.
        /// </summary>
        CloseGroup = 3
    }
}
