namespace CK.Monitoring
{
    /// <summary>
    /// Root log event handler interface.
    /// </summary>
    public interface IGrandOutputSink
    {
        /// <summary>
        /// Handles a log entry.
        /// </summary>
        /// <param name="logEvent">The input log entry.</param>
        void Handle( InputLogEntry logEvent );
    }
}
