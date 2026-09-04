The log sink for CK.ActivityMonitor.

Extends CK.ActivityMonitor with outputs and log entry serialization. The main type is `GrandOutput`:
it centrally collects the entries emitted by every ActivityMonitor and dispatches them to
configurable handlers - Console, TextFile, and BinaryFile (the lossless `.ckmon` format, optionally
gzipped) - which can be created, updated and removed while the application runs.

Also contains the reading side: `LogReader` and `MultiLogReader` rebuild a full activity map from
`.ckmon` files.

The GrandOutput and its handlers are normally configured from the "CK-Monitoring" section of the
application configuration rather than through this API.
