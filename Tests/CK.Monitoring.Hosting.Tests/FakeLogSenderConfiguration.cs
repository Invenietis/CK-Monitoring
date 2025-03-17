namespace CK.Monitoring.Hosting.Tests;

public sealed class FakeLogSenderConfiguration : Handlers.IBaseLogSenderConfiguration
{
    public string? Target { get; set; }

    public int InitialBufferSize { get; set; } = 5;

    public int LostBufferSize { get; set; } = 3;

    public IHandlerConfiguration Clone() => new FakeLogSenderConfiguration()
    {
        Target = Target,
        InitialBufferSize = InitialBufferSize,
        LostBufferSize = LostBufferSize
    };
}
