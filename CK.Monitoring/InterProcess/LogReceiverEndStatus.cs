namespace CK.Monitoring.InterProcess
{
    /// <summary>
    /// Defines the final status of a <see cref="SimpleLogPipeReceiver"/>.
    /// </summary>
    public enum LogReceiverEndStatus
    {
        /// <summary>
        /// The receiver is still running.
        /// </summary>
        None,

        /// <summary>
        /// The client sent its "goodbye" message.
        /// </summary>
        Normal,

        /// <summary>
        /// The client stopped without sending the "goodbye" message.
        /// </summary>
        MissingEndMarker,

        /// <summary>
        /// An error occurred.
        /// </summary>
        Error
    }
}
