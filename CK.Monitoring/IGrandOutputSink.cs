namespace CK.Monitoring
{
    /// <summary>
    /// Root log event handler interface.
    /// </summary>
    public interface IGrandOutputSink
    {
        /// <summary>
        /// Handles a log event.
        /// </summary>
        /// <param name="logEvent">The log event.</param>
        void Handle( GrandOutputEventInfo logEvent );
    }

}
