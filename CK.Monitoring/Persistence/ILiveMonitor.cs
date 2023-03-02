using System;
using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// Exposes thread-safe view of a monitor that has been discovered
    /// by <see cref="MultiLogReader"/>.
    /// </summary>
    public interface ILiveMonitor
    {
        /// <summary>
        /// Gets the monitor identifier.
        /// </summary>
        string MonitorId { get; }

        /// <summary>
        /// Gets the identity card if some log entries from this monitor
        /// have <see cref="IdentityCard.IdentityCardFull"/> or <see cref="ActivityMonitorSimpleSenderExtension.IdentityCard.IdentityCardUpdate"/>.
        /// </summary>
        IdentityCard? IdentityCard { get; }

        /// <summary>
        /// Call back called once the <see cref="IdentityCard"/> is available
        /// or immediately if it's already present.
        /// </summary>
        /// <param name="action">The action called with this live monitor.</param>
        void OnIdentityCardCreated( Action<ILiveMonitor> action );
    }


}
