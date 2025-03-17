using CK.Core;

namespace CK.Monitoring;

public static class MailAlerter
{
    /// <summary>
    /// Tag to use to send the log as a mail.
    /// </summary>
    public static CKTrait SendMail = ActivityMonitor.Tags.Register( "SendMail" );
}
