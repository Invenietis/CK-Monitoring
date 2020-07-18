namespace CK.Monitoring.Hosting.Tests
{
    public class DemoSinkHandlerConfiguration : IHandlerConfiguration
    {
        public int Delay { get; set; }

        public IHandlerConfiguration Clone() => new DemoSinkHandlerConfiguration() { Delay = Delay };
    }

}
