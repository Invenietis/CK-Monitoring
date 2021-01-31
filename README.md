# CK-Monitoring: the GrandOutput of logs

A GrandOutput is a collector for logs sent to ActivityMonitors. Even if, technically, it is not a singleton,
we always use in practice the static `GrandOutput.Default` property that is THE GrandOutput of an ApplicationDomain.

GrandOutput can be used and configured but, once again, in practice, we use (and strongly encourage you
to do the same) the CK.Monitoring.Hosting package that encapsulates the initial configuration and
potential dynamic reconfigurations from the standard .Net configuration framework (https://docs.microsoft.com/en-us/dotnet/core/extensions/configuration)

## CK.Monitoring.Hosting

The CK-Sample-Monitoring (https://github.com/signature-opensource/CK-Sample-Monitoring) contains a simple console application
that shows how an application (that uses the generic host model - and every modern applications should use this model) can
be configured.

See the Main of this [Program.cs](https://github.com/signature-opensource/CK-Sample-Monitoring/blob/develop/MonitoringDemoApp/Program.cs).

It is mainly the standard (explained) .Net stuff except this magic line, just before the `ConfigureService` call:
```csharp
      // This configures the GrandOutput.Default and provides a scoped IActivityMonitor to the DI.
      // By default, the section name is "Monitoring".
      .UseMonitoring()
```

The configuration, in this demo application, is in the [appsettings.json](https://github.com/signature-opensource/CK-Sample-Monitoring/blob/develop/MonitoringDemoApp/appsettings.json)
file that details with lot of comments the "Monitoring" configuration. There is a lot of comments and detailed
information there: in practice the json below is the one one we often use:

```jsonc
  "Monitoring": {
    "GrandOutput": {
      "MinimalFilter": "Debug"
      "Handlers": {
        "Console": true,
        "TextFile": {
          "Path": "Text"
        }
      }
    }

```

This logs onto the Console and to "Logs/Text" timed folders.

This can be changed at anytime: full dynamic reconfiguration is supported as long as
the configuration provider supports it. Here, the appsettings.json file is configured
with the reloadOnChange flag: `config.AddJsonFile( "appsettings.json", optional: true, reloadOnChange: true )`

The [sample](https://github.com/signature-opensource/CK-Sample-Monitoring/blob/develop/README.md) can be used to
test and see this dynamic reconfiguration. 

## CK.Monitoring
### Simplest usage

Default usage to dump logs into text files.
The very first to do is to set the special static `LogFile.RootLogPath` that is initially null and can be set only once (it may be set
more than once but with the same path otherwise an exception is thrown).
The second and last thing to do is to call the static `GrandOutput.EnsureActiveDefault()` (here without any configuration). 
```csharp
  // Sets the absolute root of the log folder. 
  // It must be an absolute path and is typically a subfolder of the current application.
  LogFile.RootLogPath = "/RootLogPath";
  GrandOutput.EnsureActiveDefault();
```
From now on, any new ActivityMonitor logs will be routed into test files inside "/RootLogPath/Text" directory.

When `EnsureActiveDefault` is called without configuration, the default configuration of the `GrandOutput.Default` is equivalent to:
```csharp
new GrandOutputConfiguration().AddHandler(
    new Handlers.TextFileConfiguration()
    {
      Path = "Text"
    })
```
### Configuration & Dynamic reconfiguration

The default GrandOutput can be reconfigured at any time (and can also be disposed - the `GrandOutput.Default` static properties is then reset to null).
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

### Handlers and HandlerConfiguration
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

The handler is instantiated from its configuration by the `GrandOutput.CreateHandler` simple factory method (that can be changed):
```csharp
       /// <summary>
       /// Settable factory method for <see cref="IGrandOutputHandler"/>.
       /// Default implementation relies on Handlers that must be in the same 
       /// assembly and namespace as their configuration objects and named the 
       /// same without the "Configuration" suffix.
       /// </summary>
       static public Func<IHandlerConfiguration, IGrandOutputHandler> CreateHandler = config =>
       {
           string name = config.GetType().GetTypeInfo().FullName;
           if( !name.EndsWith( "Configuration" ) ) throw new Exception( $"Configuration handler type name must end with 'Configuration': {name}." );
           name = config.GetType().AssemblyQualifiedName.Replace( "Configuration,", "," );
           Type t = Type.GetType( name, throwOnError: true );
           return (IGrandOutputHandler)Activator.CreateInstance( t, new[] { config } );
       };
```
