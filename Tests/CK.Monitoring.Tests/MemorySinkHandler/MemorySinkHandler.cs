using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CK.Core;
using FluentAssertions;

namespace CK.Monitoring.Tests
{
    public sealed class MemorySinkHandler : IGrandOutputHandler
    {
        public static readonly ConcurrentQueue<string?> Texts = new ConcurrentQueue<string?>();

        int _delay;

        public MemorySinkHandler( MemorySinkHandlerConfiguration c )
        {
            _delay = c.Delay;
        }

        public ValueTask<bool> ActivateAsync( IActivityMonitor m )
        {
            return ValueTask.FromResult( true );
        }

        public ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor m, IHandlerConfiguration c )
        {
            if( c is MemorySinkHandlerConfiguration conf )
            {
                _delay = conf.Delay;
                return ValueTask.FromResult( true );
            }
            return ValueTask.FromResult( false );
        }

        public ValueTask DeactivateAsync( IActivityMonitor m )
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask HandleAsync( IActivityMonitor m, InputLogEntry logEvent )
        {
            await Task.Delay( _delay );
            Texts.Enqueue( logEvent.Text );
        }

        public ValueTask OnTimerAsync( IActivityMonitor m, TimeSpan timerSpan ) => ValueTask.CompletedTask;
    }
}
