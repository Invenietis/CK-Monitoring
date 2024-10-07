namespace CK.Monitoring.Hosting.Tests;

public class DemoSinkHandlerConfiguration : IHandlerConfiguration
{
    public IHandlerConfiguration Clone() => new DemoSinkHandlerConfiguration();
}
