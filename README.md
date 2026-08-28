<h1 align="center">
	CK-Monitoring
</h1>
<p align="center">
CK-Monitoring is the log <a href="https://en.wikipedia.org/wiki/Sink_(computing)">sink</a> for <a href="https://github.com/Invenietis/CK-ActivityMonitor">ActivityMonitor</a>s.
</p>

<a href="https://docs.microsoft.com/en-us/dotnet/csharp/"><img src="https://img.shields.io/badge/language-C%23-%23178600" title="Go To C# Documentation"></a>
[![Build status](https://ci.appveyor.com/api/projects/status/pxo8hsxuhqw3ebqa?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-monitoring) [![Licence](https://img.shields.io/github/license/signature-opensource/CK-Monitoring.svg)](LICENSE)

> ℹ️If you are not already familliar with the [ActivityMonitor](https://github.com/Invenietis/CK-ActivityMonitor), i'll suggest to read its [documentation](https://github.com/Invenietis/CK-ActivityMonitor) first.

## Packages produced by this repository

| Package | Description | Latest stable |
|---------|-------------|---------------|
| [CK.Monitoring](CK.Monitoring/README.md) | The `GrandOutput` sink itself, its handlers (Console, TextFile, BinaryFile) and the `.ckmon` persistence layer. | [![nuget](https://img.shields.io/nuget/v/CK.Monitoring.svg?label=CK.Monitoring)](https://www.nuget.org/packages/CK.Monitoring/) |
| [CK.Monitoring.Hosting](CK.Monitoring.Hosting/README.md) | Wires the `GrandOutput.Default` into the .NET Generic Host from the "CK-Monitoring" configuration section. | [![nuget](https://img.shields.io/nuget/v/CK.Monitoring.Hosting.svg?label=CK.Monitoring.Hosting)](https://www.nuget.org/packages/CK.Monitoring.Hosting/) |

Most applications only need `CK.Monitoring.Hosting`: it brings `CK.Monitoring` along and configures
it from `appsettings.json`. Start [there](CK.Monitoring.Hosting/README.md).

[CK-Sample-Monitoring](https://github.com/signature-opensource/CK-Sample-Monitoring) is a sample
repository that shows how an application is configured with `CK.Monitoring.Hosting` and how it
dynamically reacts to changes of its appsettings.
