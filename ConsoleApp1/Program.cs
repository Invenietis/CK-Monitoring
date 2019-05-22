using CK.Core;
using CK.Monitoring;
using System;

namespace ConsoleApp1
{
    class Program
    {
        static IActivityMonitor CreateMonitorAndRegisterGrandOutput( string topic, GrandOutput go )
        {
            var m = new ActivityMonitor( applyAutoConfigurations: false, topic: topic );
            go.EnsureGrandOutputClient( m );
            return m;
        }
        static void Main( string[] args )
        {
            var c = new GrandOutputConfiguration();
            c.AddHandler( new CK.Monitoring.Handlers.ConsoleConfiguration() );
            using( var g = new GrandOutput( c ) )
            {
                var m = CreateMonitorAndRegisterGrandOutput( "Hello Console!", g );
                using( m.OpenInfo( $"This is an info group." ) )
                {
                    m.Fatal( $"Ouch! a faaaaatal." );
                    m.OpenTrace( $"A trace" );
                    using( m.OpenInfo( $"This is another group (trace)." ) )
                    {
                        
                        try
                        {
                            throw new Exception();
                        }
                        catch( Exception ex )
                        {
                            m.Error( "An error occurred.", ex );
                        }
                    }
                }
                var n = CreateMonitorAndRegisterGrandOutput( "Hello Console!", g );
                n.Info( "Test test" );
                var d = CreateMonitorAndRegisterGrandOutput( "Hello Console!", g );
                d.Info( "This monitor can confuse everything now" );
                m.Info( "Because now my color changed" );
                m.Info( @"
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
░░░░░░░░░░░░░▄▄▄▄▄▄▄░░░░░░░░░
░░░░░░░░░▄▀▀▀░░░░░░░▀▄░░░░░░░
░░░░░░░▄▀░░░░░░░░░░░░▀▄░░░░░░
░░░░░░▄▀░░░░░░░░░░▄▀▀▄▀▄░░░░░
░░░░▄▀░░░░░░░░░░▄▀░░██▄▀▄░░░░
░░░▄▀░░▄▀▀▀▄░░░░█░░░▀▀░█▀▄░░░
░░░█░░█▄▄░░░█░░░▀▄░░░░░▐░█░░░
░░▐▌░░█▀▀░░▄▀░░░░░▀▄▄▄▄▀░░█░░
░░▐▌░░█░░░▄▀░░░░░░░░░░░░░░█░░
░░▐▌░░░▀▀▀░░░░░░░░░░░░░░░░▐▌░
░░▐▌░░░░░░░░░░░░░░░▄░░░░░░▐▌░
░░▐▌░░░░░░░░░▄░░░░░█░░░░░░▐▌░
░░░█░░░░░░░░░▀█▄░░▄█░░░░░░▐▌░
░░░▐▌░░░░░░░░░░▀▀▀▀░░░░░░░▐▌░
░░░░█░░░░░░░░░░░░░░░░░░░░░█░░
░░░░▐▌▀▄░░░░░░░░░░░░░░░░░▐▌░░
░░░░░█░░▀░░░░░░░░░░░░░░░░▀░░░
░░░░░░░░░░░░░░░░░░░░░░░░░░░░░" );
            }
        }
    }
}
