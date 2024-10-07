using CK.Core;

namespace CK.Monitoring.Handlers;


/// <summary>
/// Required configuration interface of <see cref="BaseLogSender{TConfiguration}"/>.
/// </summary>
public interface IBaseLogSenderConfiguration : IHandlerConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of entries that will be kept in memory while waiting
    /// for the <see cref="CoreApplicationIdentity.Instance"/> to be available.
    /// </summary>
    int InitialBufferSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of entries that will be kept in memory
    /// when logs cannot be emitted.
    /// </summary>
    int LostBufferSize { get; set; }
}
