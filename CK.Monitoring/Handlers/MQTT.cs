using CK.Core;
using CK.MQTT;
using CK.MQTT.Client;
using CK.MQTT.LowLevelClient.PublishPackets;
using Microsoft.IO;
using System.Text;
using System.Threading.Tasks;

namespace CK.Monitoring.Handlers.MQTT
{
    public sealed class MQTT : BaseLogSender<MQTTConfiguration>
    {
        class MQTTSender : ISender
        {
            readonly MqttClientAgent _client;
            bool _connected = true;
            public bool IsActuallyConnected => _client.IsConnected;
            public QualityOfService QoS { get; set; }


            public MQTTSender( MqttClientAgent client, QualityOfService qos )
            {
                _client = client;
                QoS = qos;
            }

            static readonly Encoding _encoding = new UTF8Encoding();
            public async ValueTask<bool> TrySendAsync( IActivityMonitor monitor, IMulticastLogEntry logEvent )
            {
                var mem = (RecyclableMemoryStream)Util.RecyclableStreamManager.GetStream( "CK.Monitoring.MQTT" );
                CKBinaryWriter bw = new( mem, _encoding, false );
                logEvent.WriteLogEntry( bw );
                mem.Position = 0;
                await _client!.PublishAsync(
                    new StreamApplicationMessage(
                        mem,
                        false,
                        $"ck-log/{logEvent.GrandOutputId}",
                        QoS,
                        false
                    )
                );
                return _connected;
            }

            public void SetDisconnected() => _connected = false;

            public async ValueTask DisposeAsync()
            {
                await _client.DisconnectAsync( true, true );
                await _client.DisposeAsync();
            }
        }

        MqttClientAgent _client = null!;
        public MQTT( MQTTConfiguration config ) : base( config )
        {
        }

        public override async ValueTask<bool> ActivateAsync( IActivityMonitor monitor )
        {
            var splitted = Configuration.ConnectionString.Split( ':' );
            var channel = new TcpChannel( splitted[0], int.Parse( splitted[1] ) );
            var config = new Mqtt3ClientConfiguration()
            {
                KeepAliveSeconds = 0,
                DisconnectBehavior = DisconnectBehavior.AutoReconnect,
                Credentials = new MqttClientCredentials( "ck-log-" + CoreApplicationIdentity.InstanceId, true ),
                ManualConnectBehavior = Configuration.FirstConnectBehavior
            };
            _client = new MqttClientAgent( ( s ) => new LowLevelMqttClient( ProtocolConfiguration.Mqtt3, config, s, channel ) );
            var res = await _client.ConnectAsync( null );
            if( res.Status != ConnectStatus.Successful && res.Status != ConnectStatus.Deffered )
            {
                monitor.Error( $"Unrecoverable error while connecting :{res}" );
            }
            return await base.ActivateAsync( monitor );
        }

        public override ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor m, IHandlerConfiguration c )
        {
            if( c is not MQTTConfiguration cfg ) return new ValueTask<bool>( false );
            UpdateConfiguration( m, cfg );
            var sender = _sender;
            if( sender != null ) sender.QoS = cfg.QoS;
            return new( true );
        }
        MQTTSender? _sender;
        protected override Task<ISender?> CreateSenderAsync( IActivityMonitor monitor )
        {
            if( !_client.IsConnected ) return Task.FromResult<ISender?>( null );
            var newSender = new MQTTSender( _client, Configuration.QoS );
            _sender = newSender;
            return Task.FromResult<ISender?>( newSender );
        }

        public override async ValueTask DeactivateAsync( IActivityMonitor m )
        {
            await base.DeactivateAsync( m );
            await _client!.DisconnectAsync( true );
        }
    }
}
