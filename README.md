# CK-Monitoring

Default usage to dump logs into text files
```csharp
  SystemActivityMonitor.RootLogPath = "C:\\RootLogPath";
  GrandOutput.EnsureActiveDefault(null);
```
It is equivalent to
```csharp
new GrandOutputConfiguration().AddHandler( new Handlers.TextFileConfiguration() { Path = "Text" })
```
Advanced usage with multiple registered handlers
```csharp
  // Sets the absolute root of the log folder. 
  // It must be an absolute path and is typically a subfolder of the current application.
  SystemActivityMonitor.RootLogPath = "C:\\RootLogPath";
  // Creates a configuration object.
  var conf = new GrandOutputConfiguration()
                  .SetTimerDuration( TimeSpan.FromSeconds(1)) // 500ms is the default value.
                  .AddHandler(new Handlers.BinaryFileConfiguration()
                  {
                      Path = "OutputGzip",
                      UseGzipCompression = true
                  })
                  .AddHandler(new Handlers.BinaryFileConfiguration()
                  {
                      Path = "OutputRaw",
                      UseGzipCompression = false
                  }).AddHandler(new Handlers.TextFileConfiguration()
                  {
                      Path = "Text",
                      MaxCountPerFile = 500
                  });
  // Initializes the GrandOutput.Default singleton with the configuration object.
  GrandOutput.EnsureActiveDefault(conf);
```
