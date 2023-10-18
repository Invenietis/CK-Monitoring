using System;
using System.Threading.Tasks;
using CK.Core;
using FluentAssertions;

namespace CK.Monitoring.Tests
{
    public sealed class SlowSinkHandler : IGrandOutputHandler
    {
        int _delay;
        public static volatile int ActivatedDelay;

        public SlowSinkHandler( SlowSinkHandlerConfiguration c )
        {
            _delay = c.Delay;
        }

        public ValueTask<bool> ActivateAsync( IActivityMonitor m )
        {
            ActivatedDelay = _delay;
            return ValueTask.FromResult( true );
        }

        public ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor m, IHandlerConfiguration c )
        {
            if( c is SlowSinkHandlerConfiguration conf )
            {
                _delay = conf.Delay;
                ActivatedDelay = _delay;
                return ValueTask.FromResult( true );
            }
            return ValueTask.FromResult( false );
        }

        public ValueTask DeactivateAsync( IActivityMonitor m )
        {
            ActivatedDelay = -1;
            return ValueTask.CompletedTask;
        }

        public async ValueTask HandleAsync( IActivityMonitor m, InputLogEntry logEvent )
        {
            _delay.Should().BeGreaterOrEqualTo( 0 );
            _delay.Should().BeLessThan( 1000 );
            await Task.Delay( _delay );
        }

        public ValueTask OnTimerAsync( IActivityMonitor m, TimeSpan timerSpan ) => ValueTask.CompletedTask;
    }
}
