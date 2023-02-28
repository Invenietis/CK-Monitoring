<h1 align="center">
	CK-Monitoring
</h1>
<p align="center">
CK-Monitoring is the log <a href="https://en.wikipedia.org/wiki/Sink_(computing)">sink</a> for <a href="https://github.com/Invenietis/CK-ActivityMonitor">ActivityMonitor</a>s.
</p>



<a href="https://docs.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/language-C%23-%23178600" title="Go To C# Documentation"></a>
[![Build status](https://ci.appveyor.com/api/projects/status/pxo8hsxuhqw3ebqa?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-monitoring) [![Licence](https://img.shields.io/github/license/Invenietis/CK-Monitoring.svg)](https://github.com/Invenietis/CK-Monitoring/blob/develop/LICENSE)

> ℹ️If you are not already familliar with the [ActivityMonitor](https://github.com/Invenietis/CK-ActivityMonitor), i'll suggest to read its [documentation](https://github.com/Invenietis/CK-ActivityMonitor) first.

## Packages produced by this repository

|Package Name| Release | CI |
|------------|--------|---------|
|CK.Monitoring|[![Release feed on nuget.org](https://buildstats.info/nuget/ck.monitoring)](https://www.nuget.org/packages/CK.Monitoring/)| [![CK.Monitoring package in NetCore3 feed in Azure Artifacts](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=Azure%20Feed&prefix=v&query=%24.value%5B0%5D.version&url=https%3A%2F%2Ffeeds.dev.azure.com%2FSignature-OpenSource%2FFeeds%2F_apis%2Fpackaging%2FFeeds%2F608aa0cb-2004-455c-bde1-e89efb61da35%2Fpackages%2F31dbaee1-02b0-4c29-bd37-5a7c2ddf26a8%2Fversions%3FincludeUrls%3Dfalse%26isDeleted%3Dfalse)](https://dev.azure.com/Signature-OpenSource/Feeds/_packaging?_a=package&feed=NetCore3&view=versions&package=CK.Monitoring&protocolType=NuGet)|
|CK.Monitoring.Hosting|[![Release feed on nuget.org](https://buildstats.info/nuget/ck.monitoring.hosting)](https://www.nuget.org/packages/CK.Monitoring.Hosting/)| [![CK.Monitoring package in NetCore3 feed in Azure Artifacts](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=Azure%20Feed&prefix=v&query=%24.value%5B0%5D.version&url=https%3A%2F%2Ffeeds.dev.azure.com%2FSignature-OpenSource%2FFeeds%2F_apis%2Fpackaging%2FFeeds%2F608aa0cb-2004-455c-bde1-e89efb61da35%2Fpackages%2F90cbc762-2cf1-4665-9e5d-7a38aedf5cd1%2Fversions%3FincludeUrls%3Dfalse%26isDeleted%3Dfalse)](https://dev.azure.com/Signature-OpenSource/Feeds/_packaging?_a=package&feed=NetCore3&view=versions&package=CK.Monitoring.Hosting&protocolType=NuGet)|
## Getting Started

A GrandOutput is a collector for logs sent to ActivityMonitors. Even if, technically, it is not a singleton,
we always use in practice the static `GrandOutput.Default` property.

### Creating a GrandOutput
Most of the times, you will need only one GrandOutput.
You can get one by:

#### Using the [.NET Generic Host](https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host)
The Generic Host is a great base for any app, this is what you will probably use most of the time.
You will need the CK.Monitoring.Hosting NuGet package.
Now, you can add this line:

```diff
using Microsoft.Extensions.Hosting;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
+           .UseCKMonitoring()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<Worker>();
            });
}
```
Place this line so it run before any ActivityMonitor is instantiated.
This will configures the GrandOutput.Default and provides a scoped IActivityMonitor to the DI.

> ℹ️ An activity monitor is available on `IHostBuilder` and `HostBuilderContext`: simply call 
> the `GetBuilderMonitor()` extension method on them. This monitor will write its logs as soon as the
> GrandOuptout and its configured handlers will be available.


#### Manually by calling `GrandOutput.EnsureActiveDefault()` (Advanced)
⚠ This is an advanced usage, skip this part if you want to configure your GrandOutput.  

Simply call

```csharp
GrandOutput.EnsureActiveDefault();
```
before any ActivityMonitor is instantiated.

### Configuring the GrandOutput
The GrandOutput will output the logs in it's configured handlers.
CK.Monitoring.Hosting allow you to configure the GrandOutput with a configuration file or any other means thanks to
standard [configuration providers](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration-providers).

If you do not use CK.Monitoring.Hosting, you will have to manually configure it.

The standard handlers (included in CK.Monitoring assembly) are:

|Handlers|Write logs to|Usages|Metadata|
|--------|-------------|------|--------|
|BinaryFile|binary file (extension `.ckmon`), optionally compressed.|To be programmatically read.|All of it.|
|TextFile| a text files.|To read when no console can be shown, or when persistence is needed. When developing or in production.|log, date, exceptions, monitor ID, and loglevel.|
|Console|the console. |To read the program output when developing. Doesn't persist the logs.|log, date, exceptions, monitor ID, and loglevel.|

Now, you can configure your GrandOutput:

#### With CK.Monitoring.Hosting and the .NET Generic Host
`UseCKMonitoring()` uses the configuration section named "CK-Monitoring".
Using the json configuration provider, a typical configuration is:

```json
{
  "CK-Monitoring": {
    "GrandOutput": {
      "MinimalFilter": "Debug",
      "Handlers": {
        "Console": true,
        "TextFile": {
          "Path": "Text"
        }
      }
    }
  }
}
```
This is a configuration we often use, this logs onto the Console and to "Logs/Text" timed folders.
You can read a fully explained configuration file in [appsettings.json](https://github.com/signature-opensource/CK-Sample-Monitoring/blob/develop/MonitoringDemoApp/appsettings.json)
in the [CK-Sample-Monitoring](https://github.com/signature-opensource/CK-Sample-Monitoring).

> ℹ️ `UseMonitoring()` support dynamically changing configuration.

> ℹ️ [CK-Sample-Monitoring](https://github.com/signature-opensource/CK-Sample-Monitoring) is a sample repository that shows how an application can be configured 
> with CK.Monitoring.Hosting. The demo application dynamically reacts to the change of the appsettings.

### Manually
⚠ This is an advanced usage.  

As we saw earlier, if you instantiate the GrandOutput yourself, you should call `EnsureActiveDefault()`.
When `EnsureActiveDefault()` is called without configuration, the default configuration of the `GrandOutput.Default` is equivalent to:
```csharp
new GrandOutputConfiguration().AddHandler(
    new Handlers.TextFileConfiguration()
    {
      Path = "Text"
    })
```

You can parameterize where the root path of the log folders.
For this, set `LogFile.RootLogPath` that is initially null and can be set only once.
You should do that before calling `GrandOutput.EnsureActiveDefault()`:

```csharp
  // Sets the absolute root of the log folder. 
  // It must be an absolute path and is typically a subfolder of the current application.
  LogFile.RootLogPath = "/RootLogPath";
  GrandOutput.EnsureActiveDefault();
```
From now on, any new ActivityMonitor logs will be routed into text files inside "/RootLogPath/Text" directory.

The GrandOutput can be reconfigured at any time (and can also be disposed - the `GrandOutput.Default` static properties is then reset to null).
Reconfigurations handles create/update/delete of currently running handlers based on a key (an identity) that
depends on the type of each handlers (for "file handlers" for instance, the `Path` is the key).

```csharp
  // Sets the absolute root of the log folder. 
  // It must be an absolute path and is typically a subfolder of the current application.
  LogFile.RootLogPath = System.IO.Path.Combine( AppContext.BaseDirectory, "Logs" );
  // Creates a configuration object.
  var conf = new GrandOutputConfiguration()
                  .SetTimerDuration( TimeSpan.FromSeconds(1) ) // 500ms is the default value.
                  .AddHandler( new Handlers.BinaryFileConfiguration()
                  {
                      Path = "OutputGzip",
                      UseGzipCompression = true
                  })
                  .AddHandler( new Handlers.BinaryFileConfiguration()
                  {
                      Path = "OutputRaw",
                      UseGzipCompression = false
                  }).AddHandler( new Handlers.TextFileConfiguration()
                  {
                      Path = "Text",
                      MaxCountPerFile = 500
                  });
  // Initializes the GrandOutput.Default singleton with the configuration object.
  GrandOutput.EnsureActiveDefault( conf );
```

### Implementing a GrandOutput client
⚠ This is an advanced usage.

#### The IGrandOutputHandler

The `IGrandOutputHandler` that all handlers implement is a simple interface:
```csharp
  /// <summary>
  /// Handler interface.
  /// Object implementing this interface must expose a public constructor that accepts
  /// its associated <see cref="IHandlerConfiguration"/> object.
  /// </summary>
  public interface IGrandOutputHandler
  {
      /// <summary>
      /// Prepares the handler to receive events.
      /// This is called before any event will be received.
      /// </summary>
      /// <param name="m">The monitor to use.</param>
      /// <returns>True on success, false on error (this handler will not be added).</returns>
      ValueTask<bool> ActivateAsync( IActivityMonitor m );

      /// <summary>
      /// Called on a regular basis.
      /// Enables this handler to do any required housekeeping.
      /// </summary>
      /// <param name="m">The monitor to use.</param>
      /// <param name="timerSpan">Indicative timer duration.</param>
      ValueTask OnTimerAsync( IActivityMonitor m, TimeSpan timerSpan );

      /// <summary>
      /// Handles a log event.
      /// </summary>
      /// <param name="m">The monitor to use.</param>
      /// <param name="logEvent">The log event.</param>
      ValueTask HandleAsync( IActivityMonitor m, InputEntry logEvent );

      /// <summary>
      /// Attempts to apply configuration if possible.
      /// The handler must check the type of the given configuration and any key configuration
      /// before accepting it and reconfigures it (in such case, true must be returned).
      /// If this handler considers that this new configuration does not apply to itself, it must return false.
      /// </summary>
      /// <param name="m">The monitor to use.</param>
      /// <param name="c">Configuration to apply.</param>
      /// <returns>True if the configuration applied.</returns>
      ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor m, IHandlerConfiguration c );

      /// <summary>
      /// Closes this handler.
      /// This is called after the handler has been removed.
      /// </summary>
      /// <param name="m">The monitor to use.</param>
      ValueTask DeactivateAsync( IActivityMonitor m );
  }
```
#### And the IHandlerConfiguration
Handler configurations must fulfill this even simpler contract:

```csharp
    /// <summary>
    /// Configuration interface.
    /// </summary>
    public interface IHandlerConfiguration
    {
        /// <summary>
        /// Must return a deep clone of this configuration object.
        /// </summary>
        /// <returns>A clone of this object.</returns>
        IHandlerConfiguration Clone();
    }
```

⚠ Security considerations
---
GrandOutput configuration and handler configurations must not be serialized and exchanged with the external world.
They must remain local, like a hidden implementation detail of the running host.

If a kind of "remote log configuration feature" is needed, it must be done though specific code and only strictly controlled
changes must be allowed.

#### Required conventions

To ease configuration, choose a relevant name for the handler: for instance "MailAlerter".

- The assembly that implements the handler and its configuration must be: "CK.Monitoring.MailAlerterHandler" (file `CK.Monitoring.MailAlerterHandler.dll`).
- The handler and its configuration must both be in "CK.Monitoring.Handlers" namespace.
- The configuration type name must be: "MailAlerterConfiguration" (full name: "CK.Monitoring.Handlers.MailAlerterConfiguration").
- The handler type name must be: "MailAlerter" (full name: "CK.Monitoring.Handlers.MailAlerter").

```c#
namespace CK.Monitoring.Handlers
{
  public class MailAlerterConfiguration : IHandlerConfiguration
  {
    public string? Email { get; set; }
    //...
  }

  public class MailAlerter : IGrandOutputHandler
  {
    MailAlerterConfiguration _config;

    public DemoSinkHandler( MailAlerterConfiguration c )
    {
      _config = c;
    }

    //...
  }
}
```

By following these conventions, the following configuration (using CK.Monitorig.Hosting with json configuration provider):
```json
{
  "CK-Monitoring": {
    "GrandOutput": {
      "Handlers": {
        "Console": true,
        "MailAlerter": {
          "Email": "stupid-dev@signature-code.com"
        }
      }
    }
  }
}
```
Will automatically tries to load the "CK.Monitoring.MailAlerterHandler" assembly (it must be in the application's binary folder
of course), instantiate the configuration, the handler and activates it.

Sample code: [MailAlerterConfiguration](Tests/CK.Monitoring.MailAlerterHandler/Handler/MailAlerterConfiguration.cs) and [MailAlerter](Tests/CK.Monitoring.MailAlerterHandler/Handler/MailAlerter.cs).

[![Build history](https://buildstats.info/appveyor/chart/Signature-OpenSource/ck-monitoring?buildCount=100)](https://ci.appveyor.com/project/Signature-OpenSource/ck-monitoring)
