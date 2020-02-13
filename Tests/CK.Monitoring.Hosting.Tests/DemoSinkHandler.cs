using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CK.Monitoring.Hosting.Tests
{

    public class DemoSinkHandler : IGrandOutputHandler
    {
        DemoSinkHandlerConfiguration _config;

        public static int ActivateCount;
        public static int ApplyConfigurationCount;
        public static ConcurrentBag<GrandOutputEventInfo> LogEvents = new ConcurrentBag<GrandOutputEventInfo>();
        public static int DeactivateCount;
        public static int OnTimerCount;

        public static void Reset()
        {
            ActivateCount = 0;
            ApplyConfigurationCount = 0;
            LogEvents.Clear();
            DeactivateCount = 0;
            OnTimerCount = 0;
        }

        public DemoSinkHandler( DemoSinkHandlerConfiguration c )
        {
            _config = c;
        }

        public bool Activate( IActivityMonitor m )
        {
            m.Info( "DemoSinkHandler: Activation!" );
            Interlocked.Increment( ref ActivateCount );
            return true;
        }

        public bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c )
        {
            var conf = c as DemoSinkHandlerConfiguration;
            if( conf != null )
            {
                m.Info( "DemoSinkHandler: ApplyConfiguration!" );
                Interlocked.Increment( ref ApplyConfigurationCount );
                return true;
            }
            return false;
        }

        public void Handle( IActivityMonitor m, GrandOutputEventInfo logEvent )
        {
            LogEvents.Add( logEvent );
        }

        public void Deactivate( IActivityMonitor m )
        {
            m.Info( "DemoSinkHandler: Deactivation!" );
            Interlocked.Increment( ref DeactivateCount );
        }

        public void OnTimer( IActivityMonitor m, TimeSpan timerSpan )
        {
            m.Info( "DemoSinkHandler: OnTimer!" );
            Interlocked.Increment( ref OnTimerCount );
        }

    }

}
