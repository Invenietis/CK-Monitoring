namespace CK.Monitoring.Tests
{
    public sealed class SlowSinkHandlerConfiguration : IHandlerConfiguration
    {
        public int Delay { get; set; }

        public IHandlerConfiguration Clone() => new SlowSinkHandlerConfiguration() { Delay = Delay };
    }
}
