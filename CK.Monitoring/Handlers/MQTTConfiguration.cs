using CK.Monitoring.Handlers;
using CK.MQTT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Monitoring.Handlers.MQTT
{
    public class MQTTConfiguration : IBaseLogSenderConfiguration
    {
        /// <summary>
        /// The connection string that the client will use to connect to an mqtt server.
        /// Right now only the following format is supported: "hostname:port".
        /// </summary>
        public string ConnectionString { get; set; } = null!;
        /// <summary>
        /// This will be removed in the future and only allow to test our MQTT client resilliency.
        /// It default to QualityOfService.ExactlyOnce, which add an non-negligible overhead.
        /// </summary>
        public QualityOfService QoS { get; set; } = QualityOfService.ExactlyOnce;
        public ManualConnectBehavior FirstConnectBehavior { get; set; } = ManualConnectBehavior.TryOnceThenRetryInBackground;

        public int InitialBufferSize { get; set; } = 500;
        public int LostBufferSize { get; set; } = 500;

        public IHandlerConfiguration Clone()
        {
            return new MQTTConfiguration()
            {
                ConnectionString = ConnectionString,
                QoS = QoS,
                InitialBufferSize = InitialBufferSize,
                LostBufferSize = LostBufferSize
            };
        }
    }
}
