
namespace CK.Monitoring
{
    /// <summary>
    /// Immutable capture of a log <see cref="Entry"/> and its <see cref="Offset"/>.
    /// </summary>
    public readonly struct MulticastLogEntryWithOffset
    {
        /// <summary>
        /// The log entry.
        /// </summary>
        public readonly IFullLogEntry Entry;
        
        /// <summary>
        /// The entry's offset.
        /// </summary>
        public readonly long Offset;

        /// <summary>
        /// Initializes a new <see cref="MulticastLogEntryWithOffset"/>.
        /// </summary>
        /// <param name="e">The entry.</param>
        /// <param name="o">The offset.</param>
        public MulticastLogEntryWithOffset( IFullLogEntry e, long o )
        {
            Entry = e;
            Offset = o;
        }
    }

}
