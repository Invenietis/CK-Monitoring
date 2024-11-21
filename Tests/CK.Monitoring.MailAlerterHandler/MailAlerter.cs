using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Monitoring;

public static class MailAlerter
{
    /// <summary>
    /// Tag to use to send the log as a mail.
    /// </summary>
    public static CKTrait SendMail = ActivityMonitor.Tags.Register( "SendMail" );
}
