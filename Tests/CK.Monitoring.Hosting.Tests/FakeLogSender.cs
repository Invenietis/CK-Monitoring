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
        // Only logs tagged with this are considered.
        public static readonly CKTrait TestTag = ActivityMonitor.Tags.Context.FindOrCreate( "OnlyThisIsHandled" );
        public static FakeLogSender? ActivatedSender = null;
        public static volatile bool FakeSenderCanBeCreated;
        public static volatile bool FakeSenderPersistentError;
        public static volatile bool FakeSenderImplIsActuallyConnected;
        public static volatile bool FakeSenderImplTrySendSuccess;
        public static readonly List<string> LogSent = new List<string>();

        public static void Reset()
        {
            FakeSenderCanBeCreated = false;
            FakeSenderPersistentError = false;
            FakeSenderImplIsActuallyConnected = false;
            FakeSenderImplTrySendSuccess = false;
            LogSent.Clear();
        }

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
            }

            public bool IsActuallyConnected => FakeSenderImplIsActuallyConnected;

            public bool Disposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> TrySendAsync( IActivityMonitor monitor, IMulticastLogEntry e )
            {
                if( FakeSenderImplTrySendSuccess )
                {
                    if( e.Text != null ) LogSent.Add( e.Text );
                    return ValueTask.FromResult( true );
                }
                return ValueTask.FromResult( false );
            }
        }

        protected override bool SenderCanBeCreated => FakeSenderCanBeCreated;

        public SenderImpl? FakeSender => (SenderImpl?)base.Sender;

        protected override Task<ISender?> CreateSenderAsync( IActivityMonitor monitor )
        {
            if( FakeSenderPersistentError ) return Task.FromResult<ISender?>( null );
            var s = new SenderImpl( this );
            return Task.FromResult<ISender?>( s );
        }

        public override ValueTask HandleAsync( IActivityMonitor monitor, InputLogEntry logEvent )
        {
            // Consider only TestTag logs.
            if( logEvent.Tags.IsSupersetOf( TestTag ) )
            {
                return base.HandleAsync( monitor, logEvent );
            }
            return ValueTask.CompletedTask;
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

        public override async ValueTask<bool> ActivateAsync( IActivityMonitor monitor )
        {
            if( !await base.ActivateAsync( monitor ) ) return false;
            ActivatedSender = this;
            return true;
        }

        public override ValueTask DeactivateAsync( IActivityMonitor monitor )
        {
            ActivatedSender = null;
            return base.DeactivateAsync( monitor );
        }

    }

}
