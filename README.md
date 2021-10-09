<h1 align="center">
	CK-Monitoring
</h1>
<p align="center">
CK-Monitoring is the log <a href="https://en.wikipedia.org/wiki/Sink_(computing)">sink</a> for <a href="https://github.com/Invenietis/CK-ActivityMonitor">ActivityMonitor</a>s.
</p>



<a href="https://docs.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/language-C%23-%23178600" title="Go To C# Documentation"></a>
[![Build status](https://ci.appveyor.com/api/projects/status/pxo8hsxuhqw3ebqa?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-monitoring)

> ⚠️If you are not already familliar with the [ActivityMonitor](https://github.com/Invenietis/CK-ActivityMonitor) i'll suggest to read it's [documentation](https://github.com/Invenietis/CK-ActivityMonitor) first.

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
<details>
<summary> Using the <a href="https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host">.NET Generic Host</a> <sub>[Expand]</sub>
</summary>

<p><ul>The Generic Host is a great base for any app, this is what you will probably use most of the time.
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
+           .UseMonitoring()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<Worker>();
            });
}
```
Place this line so it run before any ActivityMonitor is instancied.
This will configures the GrandOutput.Default and provides a scoped IActivityMonitor to the DI.


</li>
</p>
</details>

<details>
<summary>Manually by calling <span>
`GrandOutput.EnsureActiveDefault()`
</span> <sub>[Expand]</sub> </summary>
<p><ul>
Simply call

```csharp
GrandOutput.EnsureActiveDefault();
```
before any ActivityMonitor is instancied.</ul></p></details>
### Configuring the GrandOutput
The GrandOutput will output the logs in it's configured handlers.
CK.Monitoring.Hosting allow you to configure the GrandOutput with a config file.
If you do not use CK.Monitoring.Hosting, you will have to manually configure it.

Here are described all the differents logs handlers you can use:
|Handlers|Write logs to|Usages|Metadata|
|--------|-------------|------|--------|
|BinaryFile|a binary file, optionally compressed.|To be programmatically read.|All of it.|
|TextFile| a text files.|To read when no console can be shown, or when persistance is needed. When developing or in production.|log, date, exceptions, monitor ID, and loglevel.|
|Console|the console. |To read the program output when developing. Doesn't persist the logs.|log, date, exceptions, monitor ID, and loglevel.|

Now, you can configure your GrandOutput:
<details>
<summary> with CK.Monitoring.Hosting and the <a href="https://docs.microsoft.com/en-us/dotnet/core/extensions/generic-host">.NET Generic Host</a> <sub>[Expand]</sub></summary>
<ul> 
 `UseMonitoring()` by default will use the config section name "Monitoring".
 By default, it will use the config present in the dependency injection, but you can pass a configuration section.
 To add a configuration to your Host, follow the [Official Documentation](https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration).
If you use a json config provider, your logs config should be located in your json config like this:

```json
{
  "Monitoring": {
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
This is a config we often use, this logs onto the Console and to "Logs/Text" timed folders.
You can read a fully explained config files in the <a href="https://github.com/signature-opensource/CK-Sample-Monitoring/blob/develop/MonitoringDemoApp/appsettings.json"> appsettings.json</a> in the <a href="https://github.com/signature-opensource/CK-Sample-Monitoring">CK-Sample-Monitoring</a>.

> :information_source: `UseMonitoring()` support dynamically changing configuration.

> :information_source: <a href="https://github.com/signature-opensource/CK-Sample-Monitoring">CK-Sample-Monitoring</a> is a sample repository that shows how an application can be configured with CK.Monitoring.Hosting.


</ul>
</details>

<details>
<summary>Manually</a> <sub>[Expand]</sub></summary>
<ul>
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
depends on the type of each handlers (for "file handlers" for instance, the Path is the key).

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

</ul>
</details>


### Implementing a GrandOutput client
<details>
<summary>The IGrandOutputHandler <sub>[Expand]</sub></summary>
<ul>

The `IGrandOutputHandler` that all handlers implement is a very simple interface:
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
        bool Activate( IActivityMonitor m );

        /// <summary>
        /// Called on a regular basis.
        /// Enables this handler to do any required housekeeping.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="timerSpan">Indicative timer duration.</param>
        void OnTimer( IActivityMonitor m, TimeSpan timerSpan );

        /// <summary>
        /// Handles a log event.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="logEvent">The log event.</param>
        void Handle( IActivityMonitor m, GrandOutputEventInfo logEvent );

        /// <summary>
        /// Attempts to apply configuration if possible.
        /// The handler must check the type of the given configuration and any key configuration
        /// before accepting it and reconfigures it (in such case, true must be returned).
        /// If this handler considers that this new configuration does not apply to itself, it must return false.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="c">Configuration to apply.</param>
        /// <returns>True if the configuration applied.</returns>
        bool ApplyConfiguration( IActivityMonitor m, IHandlerConfiguration c );

        /// <summary>
        /// Closes this handler.
        /// This is called after the handler has been removed.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        void Deactivate( IActivityMonitor m );
    }
```
</ul>
</details>
<details>
<summary>And the IHandlerConfiguration<sub>[Expand]</sub>
</summary>
<ul>
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

[![Build history](https://buildstats.info/appveyor/chart/Signature-OpenSource/ck-monitoring?buildCount=100)](https://ci.appveyor.com/project/Signature-OpenSource/ck-monitoring)
