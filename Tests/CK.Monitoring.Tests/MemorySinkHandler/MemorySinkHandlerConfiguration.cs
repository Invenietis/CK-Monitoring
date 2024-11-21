namespace CK.Monitoring.Tests;

public sealed class MemorySinkHandlerConfiguration : IHandlerConfiguration
{
    public int Delay { get; set; }

    public IHandlerConfiguration Clone() => new MemorySinkHandlerConfiguration() { Delay = Delay };
}
