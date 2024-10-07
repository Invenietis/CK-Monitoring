using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Monitoring.Handlers;

/// <summary>
/// Stupid handler implementation.
/// </summary>
public class MailAlerter : IGrandOutputHandler
{
    MailAlerterConfiguration _c;

    // Just fo test... See HostingTests.finding_MailAlerter_handler_by_conventions_Async().
    public static string? LastMailSent;

    public MailAlerter( MailAlerterConfiguration c )
    {
        _c = c;
    }

    public ValueTask<bool> ActivateAsync( IActivityMonitor m ) => ValueTask.FromResult( _c.CheckValidity( m ) );

    public ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor m, IHandlerConfiguration c )
    {
        if( c is MailAlerterConfiguration newC && newC.Email == _c.Email )
        {
            _c = newC;
            return ValueTask.FromResult( true );
        }
        return ValueTask.FromResult( false );
    }

    public ValueTask DeactivateAsync( IActivityMonitor m ) => default;

    public ValueTask HandleAsync( IActivityMonitor m, InputLogEntry logEvent )
    {
        if( logEvent.Tags.IsSupersetOf( CK.Monitoring.MailAlerter.SendMail ) )
        {
            LastMailSent = logEvent.Text;
            // Do send mail...
        }
        return default;
    }

    public ValueTask OnTimerAsync( IActivityMonitor m, TimeSpan timerSpan ) => default;
}
