using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting;


sealed class GrandOutputConfigurator
{
    readonly bool _isDefaultGrandOutput;
    GrandOutput _target;
    IConfiguration _configuration;
    GrandOutputLoggerAdapterProvider? _loggerProvider;
    IDisposable? _changeToken;

    /// <summary>
    /// Simply dispose the associated GrandOutput when the host stops.
    /// <para>
    /// This is registered in the Builder.Services for independent GrandOutputs.
    /// The default GrandOutput is bound to AppDomain.CurrentDomain.DomainUnload and AppDomain.CurrentDomain.ProcessExit.
    /// </para>
    /// </summary>
    sealed class HostedService : IHostedLifecycleService
    {
        readonly GrandOutput _grandOutput;

        public HostedService( GrandOutput grandOutput ) => _grandOutput = grandOutput;

        public ValueTask DisposeAsync() => _grandOutput.DisposeAsync();

        public Task StartingAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        Task IHostedService.StartAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task StartedAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task StoppingAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        Task IHostedService.StopAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task StoppedAsync( CancellationToken cancellationToken )
        {
            _grandOutput.Stop();
            return _grandOutput.RunningTask;
        }

    }

    public GrandOutputConfigurator( IHostApplicationBuilder builder, bool isDefaultGrandOutput )
    {
        _configuration = builder.Configuration;
        _isDefaultGrandOutput = isDefaultGrandOutput;
        var section = builder.Configuration.GetSection( "CK-Monitoring" );
        if( LogFile.RootLogPath == null )
        {
            LogFile.RootLogPath = Path.GetFullPath( Path.Combine( builder.Environment.ContentRootPath, section["LogPath"] ?? "Logs" ) );
        }
        ApplyDynamicConfiguration( initialConfigMustWaitForApplication: true );
        builder.Services.AddSingleton<ILoggerProvider>( _loggerProvider );

        if( !isDefaultGrandOutput )
        {
            builder.Services.AddSingleton<IHostedService>( new HostedService( _target ) );
        }

        var reloadToken = section.GetReloadToken();
        _changeToken = reloadToken.RegisterChangeCallback( OnConfigurationChanged, this );

        // We do not handle CancellationTokenRegistration.Dispose here.
        // The target is disposing: everything will be discarded, included
        // this instance of initializer.
        _target.StoppedToken.Register( () =>
        {
            _changeToken.Dispose();
            _loggerProvider._running = false;
        } );
        // If the BuilderMonitor has been requested, replay its logs.
        var builderMonitor = (IActivityMonitor?)builder.Properties.GetValueOrDefault( typeof( IActivityMonitor ) );
        if( builderMonitor != null )
        {
            // Instead of relying only on the GrandOuput.Defaut, simply calls the AutoConfiguration action:
            // if more than one GrandOuput currently exists, they will receive the logs of the new Host initialization.
            ActivityMonitor.AutoConfiguration?.Invoke( builderMonitor );
            // Stops the replay.
            builderMonitor.Output.MaxInitialReplayCount = null;
        }
    }

    public GrandOutput GrandOutputTarget => _target;

    [MemberNotNull( nameof( _target ), nameof( _loggerProvider ) )]
    void ApplyDynamicConfiguration( bool initialConfigMustWaitForApplication )
    {
        var section = _configuration.GetSection( "CK-Monitoring" );
        bool dotNetLogs = !String.Equals( section["HandleDotNetLogs"], "false", StringComparison.OrdinalIgnoreCase );

        LogFilter defaultFilter = default;
        string? globalDefaultFilter = null;
        bool errorParsingGlobalDefaultFilter = false;
        if( _isDefaultGrandOutput )
        {
            ParseAndSetStaticConfigurations( _target,
                                             section,
                                             initialConfigMustWaitForApplication,
                                             out defaultFilter,
                                             out globalDefaultFilter,
                                             out errorParsingGlobalDefaultFilter );
        }

        try
        {
            GrandOutputConfiguration c = CreateConfigurationFromSection( section );
            if( _target == null )
            {
                Throw.DebugAssert( initialConfigMustWaitForApplication );
                _target = _isDefaultGrandOutput
                            ? GrandOutput.EnsureActiveDefault( c )
                            : new GrandOutput( c );
                _loggerProvider = new GrandOutputLoggerAdapterProvider( _target );
            }
            else
            {
                Throw.DebugAssert( _loggerProvider != null );
                _target.ApplyConfiguration( c, initialConfigMustWaitForApplication );
            }
        }
        catch( Exception ex )
        {
            if( _target == null )
            {
                // Using the default "Text" log configuration.
                // If this fails (!), let the exception breaks the whole initialization since this
                // is really unrecoverable.
                _target = _isDefaultGrandOutput
                            ? GrandOutput.EnsureActiveDefault()
                            : new GrandOutput();
                _loggerProvider = new GrandOutputLoggerAdapterProvider( _target );
            }
            _target.ExternalLog( Core.LogLevel.Fatal, message: $"While applying dynamic configuration.", ex );
        }
        Debug.Assert( _loggerProvider != null );
        _loggerProvider._running = dotNetLogs;

        if( _isDefaultGrandOutput )
        {
            ApplyStaticConfigurations( _target,
                                       section,
                                       initialConfigMustWaitForApplication,
                                       defaultFilter,
                                       globalDefaultFilter,
                                       errorParsingGlobalDefaultFilter );
        }

        static LogFilter SetGlobalDefaultFilter( GrandOutput target, LogFilter defaultFilter )
        {
            if( defaultFilter.Group == LogLevelFilter.None || defaultFilter.Line == LogLevelFilter.None )
            {
                target.ExternalLog( Core.LogLevel.Error, message: $"Invalid GlobalDefaultFilter = '{defaultFilter}'. using default 'Trace'." );
                defaultFilter = LogFilter.Trace;
            }
            target.ExternalLog( Core.LogLevel.Info, message: $"Configuring ActivityMonitor.DefaultFilter to GlobalDefaultFilter = '{defaultFilter}'." );
            ActivityMonitor.DefaultFilter = defaultFilter;
            return defaultFilter;
        }

        static void ParseAndSetStaticConfigurations( GrandOutput? target,
                                                     IConfigurationSection section,
                                                     bool initialConfigMustWaitForApplication,
                                                     out LogFilter defaultFilter,
                                                     out string? globalDefaultFilter,
                                                     out bool errorParsingGlobalDefaultFilter )
        {
            defaultFilter = LogFilter.Undefined;
            globalDefaultFilter = section["GlobalDefaultFilter"];
            errorParsingGlobalDefaultFilter = globalDefaultFilter != null && !LogFilter.TryParse( globalDefaultFilter, out defaultFilter );
            if( globalDefaultFilter != null && !errorParsingGlobalDefaultFilter )
            {
                if( initialConfigMustWaitForApplication )
                {
                    // On first initialization, configure the filter as early as possible.
                    if( defaultFilter.Group != LogLevelFilter.None && defaultFilter.Line != LogLevelFilter.None )
                    {
                        ActivityMonitor.DefaultFilter = defaultFilter;
                    }
                    // If the filter is invalid (a None appears), keep the default Trace.
                }
                else
                {
                    // If a GlobalDefaultFilter has been successfully parsed and we are reconfiguring and it is different than
                    // the current one, logs the change and applies the configuration.
                    if( defaultFilter != ActivityMonitor.DefaultFilter )
                    {
                        Throw.DebugAssert( "Since initialConfigMustWaitForApplication is false.", target != null );
                        defaultFilter = SetGlobalDefaultFilter( target, defaultFilter );
                    }
                }
            }
        }

        static void ApplyStaticConfigurations( GrandOutput target,
                                               IConfigurationSection section,
                                               bool initialConfigMustWaitForApplication,
                                               LogFilter defaultFilter,
                                               string? globalDefaultFilter,
                                               bool errorParsingGlobalDefaultFilter )
        {
            // Applying Tags.
            List<(CKTrait, LogClamper)>? parsedTags = null;
            foreach( var entry in section.GetSection( "TagFilters" ).GetChildren() )
            {
                if( int.TryParse( entry.Key, out var idxEntry ) )
                {
                    parsedTags = HandleTag( target, parsedTags, entry, entry["0"], entry["1"] );
                }
                else
                {
                    parsedTags = HandleTag( target, parsedTags, entry, entry.Key, entry.Value );
                }
            }
            if( parsedTags != null )
            {
                ActivityMonitor.Tags.SetFilters( parsedTags.ToArray() );
            }

            if( globalDefaultFilter != null )
            {
                // Always log the parse error, but only log and applies if this is the initial configuration.
                if( errorParsingGlobalDefaultFilter )
                {
                    target.ExternalLog( Core.LogLevel.Error, message: $"Unable to parse configuration 'GlobalDefaultFilter'. Expected \"Debug\", \"Trace\", \"Verbose\", \"Monitor\", \"Terse\", \"Release\", \"Off\" or pairs of \"{{Group,Line}}\" levels where Group or Line can be Debug, Trace, Info, Warn, Error, Fatal or Off." );
                }
                else if( initialConfigMustWaitForApplication )
                {
                    SetGlobalDefaultFilter( target, defaultFilter );
                }
            }

            static List<(CKTrait, LogClamper)>? HandleTag( GrandOutput target, List<(CKTrait, LogClamper)>? parsedTags, IConfigurationSection entry, string? name, string? filter )
            {
                if( name != null && filter != null )
                {
                    var t = ActivityMonitor.Tags.Register( name );
                    if( !t.IsEmpty && LogClamper.TryParse( filter, out var c ) )
                    {
                        parsedTags ??= new List<(CKTrait, LogClamper)>();
                        parsedTags.Add( (t, c) );
                    }
                    else
                    {
                        if( t.IsEmpty )
                            target.ExternalLog( Core.LogLevel.Warn, message: $"Ignoring TagFilters '{entry.Path}': [{name},{filter}]. Tag is empty" );
                        else target.ExternalLog( Core.LogLevel.Warn, message: $"Ignoring TagFilters '{entry.Path}': [{name},{filter}]. Unable to parse clamp value. Expected a LogFilter (followed by an optional '!'): \"Debug\", \"Trace\", \"Verbose\", \"Monitor\", \"Terse\", \"Release\", \"Off\" or pairs of \"{{Group,Line}}\" levels where Group or Line can be Debug, Trace, Info, Warn, Error, Fatal or Off." );
                    }
                }
                return parsedTags;
            }
        }
    }

    static GrandOutputConfiguration CreateConfigurationFromSection( IConfigurationSection section )
    {
        GrandOutputConfiguration c;
        var gSection = section.GetSection( nameof( GrandOutput ) );
        if( gSection.Exists() )
        {
            var ctorPotentialParams = new[] { typeof( IConfigurationSection ) };
            c = new GrandOutputConfiguration();
            gSection.Bind( c );
            var hSection = gSection.GetSection( "Handlers" );
            foreach( var hConfig in hSection.GetChildren() )
            {
                // Checks for single value and not section.
                // This is required for handlers that have no configuration properties:
                // "Handlers": { "Console": true } does the job.
                // The only case of "falseness" we consider here is "false": we ignore the key in this case.
                string? value = hConfig.Value;
                if( !String.IsNullOrWhiteSpace( value )
                    && String.Equals( value, "false", StringComparison.OrdinalIgnoreCase ) ) continue;

                // Resolve configuration type using one of two available strings:
                // 1. From "ConfigurationType" property, inside the value object
                Type? resolved = null;
                string? configTypeProperty = hConfig["ConfigurationType"];
                if( string.IsNullOrEmpty( configTypeProperty ) )
                {
                    // No ConfigurationType property:
                    // Resolve using the key, outside the value object
                    resolved = TryResolveType( hConfig.Key );
                }
                else
                {
                    // With ConfigurationType property:
                    // Try and resolve property and key, in that order
                    resolved = TryResolveType( configTypeProperty );
                    if( resolved == null )
                    {
                        resolved = TryResolveType( hConfig.Key );
                    }
                }
                if( resolved == null )
                {
                    if( string.IsNullOrEmpty( configTypeProperty ) )
                    {
                        throw new CKException( $"Unable to resolve type '{hConfig.Key}'." );
                    }
                    else
                    {
                        throw new CKException( $"Unable to resolve type '{configTypeProperty}' (from Handlers.{hConfig.Key}.ConfigurationType) or '{hConfig.Key}'." );
                    }
                }
                var ctorWithConfig = resolved.GetConstructor( ctorPotentialParams );
                object config;
                if( ctorWithConfig != null ) config = ctorWithConfig.Invoke( new[] { hConfig } );
                else
                {
                    config = Activator.CreateInstance( resolved )!;
                    hConfig.Bind( config );
                }
                c.AddHandler( (IHandlerConfiguration)config );
            }
        }
        else
        {
            c = new GrandOutputConfiguration()
                .AddHandler( new Handlers.TextFileConfiguration() { Path = "Text" } );
        }
        return c;

        static Type? TryResolveType( string name )
        {
            Type? resolved;
            if( name.Contains( ',' ) )
            {
                // It must be an assembly qualified name.
                // Weaken its name and try to load it.
                // If it fails and the name does not end with "Configuration" tries with "Configuration" suffix.
                string fullTypeName, assemblyFullName, assemblyName, versionCultureAndPublicKeyToken;
                if( SimpleTypeFinder.SplitAssemblyQualifiedName( name, out fullTypeName, out assemblyFullName )
                    && SimpleTypeFinder.SplitAssemblyFullName( assemblyFullName, out assemblyName, out versionCultureAndPublicKeyToken ) )
                {
                    var weakTypeName = fullTypeName + ", " + assemblyName;
                    resolved = SimpleTypeFinder.RawGetType( weakTypeName, false );
                    if( IsHandlerConfiguration( resolved ) ) return resolved;
                    if( !fullTypeName.EndsWith( "Configuration" ) )
                    {
                        weakTypeName = fullTypeName + "Configuration, " + assemblyName;
                        resolved = SimpleTypeFinder.RawGetType( weakTypeName, false );
                        if( IsHandlerConfiguration( resolved ) ) return resolved;
                    }
                }
                return null;
            }
            // This is a simple type name: try to find the type name in already loaded assemblies.
            var configTypes = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany( a => a.GetTypes() )
                                .Where( t => typeof( IHandlerConfiguration ).IsAssignableFrom( t ) )
                                .ToList();
            var nameWithC = name.EndsWith( "Configuration" ) ? null : name + "Configuration";
            if( name.IndexOf( '.' ) > 0 )
            {
                // It is a FullName.
                resolved = configTypes.FirstOrDefault( t => t.FullName == name
                                                            || (nameWithC != null && t.FullName == nameWithC) );
            }
            else
            {
                // There is no dot in the name.
                resolved = configTypes.FirstOrDefault( t => t.Name == name
                                                            || (nameWithC != null && t.Name == nameWithC) );
                if( resolved == null )
                {
                    // Not found in currently loaded assemblies.
                    if( nameWithC == null ) name = name.Substring( 0, name.Length - 13 );
                    var t = SimpleTypeFinder.RawGetType( $"CK.Monitoring.Handlers.{name}Configuration, CK.Monitoring.{name}Handler", false );
                    if( IsHandlerConfiguration( t ) ) resolved = t;
                }
            }
            return resolved;

            static bool IsHandlerConfiguration( Type? candidate ) => candidate != null && typeof( IHandlerConfiguration ).IsAssignableFrom( candidate );
        }

    }

    static void OnConfigurationChanged( object? obj )
    {
        Throw.DebugAssert( obj is GrandOutputConfigurator );
        var initializer = Unsafe.As<GrandOutputConfigurator>( obj );
        initializer.ApplyDynamicConfiguration( false );
        initializer.RenewChangeToken();
    }

    void RenewChangeToken()
    {
        Debug.Assert( _changeToken != null );
        // Disposes the previous change token.
        _changeToken.Dispose();
        // Reacquires the token: using this as the state keeps this object alive.
        var reloadToken = _configuration.GetReloadToken();
        _changeToken = reloadToken.RegisterChangeCallback( OnConfigurationChanged, this );
    }
}
