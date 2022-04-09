using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting.Tests
{
    public sealed class FakeLogSender : Handlers.BaseLogSender<FakeLogSenderConfiguration>
    {
        public FakeLogSender( FakeLogSenderConfiguration c )
            : base( c )
        {
        }

        public sealed class SenderImpl : ISender
        {
            readonly FakeLogSender _owner;

            public SenderImpl( FakeLogSender owner )
            {
                _owner = owner;
                LogSent = new List<string>();
            }

            public bool IsActuallyConnected { get; set; }

            public bool FakeSend { get; set; }

            public List<string> LogSent { get; }

            public bool Disposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> TrySendAsync( IActivityMonitor monitor, IMulticastLogEntry e )
            {
                if( FakeSend )
                {
                    if( e.Text != null ) LogSent.Add( e.Text );
                    return ValueTask.FromResult( true );
                }
                return ValueTask.FromResult( false );
            }
        }

        protected override bool SenderCanBeCreated => FakeSenderCanBeCreated;

        public bool FakeSenderCanBeCreated { get; set; }

        public bool FakeSenderPersistentError { get; set; }

        protected override Task<ISender?> CreateSenderAsync( IActivityMonitor monitor )
        {
            if( FakeSenderPersistentError ) return Task.FromResult<ISender?>( null );
            var s = new SenderImpl( this );
            return Task.FromResult<ISender?>( s );
        }

        public override ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor monitor, IHandlerConfiguration c )
        {
            if( c is FakeLogSenderConfiguration conf && conf.Target == Configuration.Target )
            {
                UpdateConfiguration( monitor, conf );
                return ValueTask.FromResult( true );
            }
            return ValueTask.FromResult( false );
        }
    }

}
