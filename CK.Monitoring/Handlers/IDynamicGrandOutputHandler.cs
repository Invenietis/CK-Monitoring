namespace CK.Monitoring.Handlers;

/// <summary>
/// Internal interface for <see cref="GrandOutputMemoryCollector"/>-like handlers.
/// Handling of add/remove of these dynamic handlers is done once for all
/// by the <see cref="DispatcherSink.AddDynamicHandler(IGrandOutputHandler)"/>
/// and <see cref="DispatcherSink.RemoveDynamicHandler(IDynamicGrandOutputHandler)"/>.
/// </summary>
internal interface IDynamicGrandOutputHandler
{
    IGrandOutputHandler Handler { get; }
}
