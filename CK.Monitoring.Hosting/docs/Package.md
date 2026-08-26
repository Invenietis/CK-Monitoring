Configures CK.Monitoring from the .NET Generic Host.

A single `UseCKMonitoring()` call on the host builder creates the `GrandOutput.Default` from the
"CK-Monitoring" configuration section and keeps it in sync: editing appsettings.json reconfigures the
running handlers. Microsoft.Extensions.Logging entries are routed to the same handlers.

`GetBuilderMonitor()` provides an ActivityMonitor usable during host construction, before the sink
exists: its logs are replayed once the handlers are up.
