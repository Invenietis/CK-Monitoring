using CK.Core;
using System;

namespace CK.Monitoring.Handlers;

public class MailAlerterConfiguration : IHandlerConfiguration
{
    public string? Email { get; set; }

    public IHandlerConfiguration Clone()
    {
        return new MailAlerterConfiguration()
        {
            Email = Email
        };
    }

    internal bool CheckValidity( IActivityMonitor monitor )
    {
        if( String.IsNullOrWhiteSpace( Email ) )
        {
            monitor.Error( $"Invalid Email configuration: {Email}" );
            return false;
        }
        return true;
    }
}
