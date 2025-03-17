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
using System.Threading;
using System.Threading.Tasks;

namespace CK.Monitoring.Hosting;


[Obsolete( "This one is for the IHostBuilder.", false )]
sealed class GrandOutputConfigurationInitializer
{
    /// <summary>
    /// Simply dispose the associated GrandOutput when the server close.
    /// </summary>
    sealed class HostedService : IHostedService, IDisposable
    {
        readonly GrandOutput _grandOutput;

        public HostedService( GrandOutput grandOutput )
        {
            _grandOutput = grandOutput;
        }

        public void Dispose()
        {
            _grandOutput.Stop();
        }

        public Task StartAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task StopAsync( CancellationToken cancellationToken )
        {
            _grandOutput.Stop();
            return _grandOutput.RunningTask;
        }
    }

    GrandOutput? _target;
    GrandOutputLoggerAdapterProvider? _loggerProvider;
    IConfigurationSection? _section;
    IDisposable? _changeToken;
    readonly bool _isDefaultGrandOutput;

    public GrandOutputConfigurationInitializer( GrandOutput? target )
    {
        _isDefaultGrandOutput = (_target = target) == null;
        if( target != null ) _loggerProvider = new GrandOutputLoggerAdapterProvider( target );
    }

    public void ConfigureBuilder( IHostBuilder builder, Func<IConfiguration, IConfigurationSection> configSection )
    {
        builder.ConfigureLogging( ( ctx, loggingBuilder ) =>
        {
            Debug.Assert( ReferenceEquals( ctx.Properties, builder.Properties ) );
            Initialize( ctx.HostingEnvironment, loggingBuilder, ctx.Configuration, configSection( ctx.Configuration ) );
            // If the BuilderMonitor has been requested, replay its logs.
            var builderMonitor = (IActivityMonitor?)ctx.Properties.GetValueOrDefault( typeof( IActivityMonitor ) );
            if( builderMonitor != null )
            {
                // Instead of relying only on the GrandOuput.Defaut, simply calls the AutoConfiguration action:
                // if more than one GrandOuput currently exists, they will receive the logs of the new Host initialization.
                ActivityMonitor.AutoConfiguration?.Invoke( builderMonitor );
                // Stops the replay.
                builderMonitor.Output.MaxInitialReplayCount = null;
            }
        } );
        builder.ConfigureServices( ( ctx, services ) =>
        {
            Debug.Assert( ReferenceEquals( ctx.Properties, builder.Properties ) );
            Debug.Assert( _target != null );
            services.AddHostedService( sp => new HostedService( _target ) );
            // Without fully analyzing the existing descriptors (foreach) we cannot know if: 
            //  - The ActivityMonitor type has been registered and how (Type,Type) or (Type, Factory).
            //  - The IActivityMonitor has been correctly registered: it should be a (Type, sp => GetService<ActivityMonitor>())
            //    if the ActivityMonitor has been registered or a (IActivityMonitor,ActivityMonitor) otherwise.
            //  - Same for the IParallelLogger.
            // This is not really our job here to check that kind of stuff. So we take for granted that we are the first
            // to register these and provide the right registration for a monitor:
            //  - The ActivityMonitor implementation doesn't need to be exposed.
            //  - The IParallelLogger is the one of the monitor.
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
        } );
    }

    void Initialize( IHostEnvironment env, ILoggingBuilder dotNetLogs, IConfiguration globalConfiguration, IConfigurationSection section )
    {
        _section = section;
        if( LogFile.RootLogPath == null )
        {
            LogFile.RootLogPath = Path.GetFullPath( Path.Combine( env.ContentRootPath, _section["LogPath"] ?? "Logs" ) );
        }
        ApplyDynamicConfiguration( initialConfigMustWaitForApplication: true );
        dotNetLogs.AddProvider( _loggerProvider );

        var reloadToken = _section.GetReloadToken();
        _changeToken = reloadToken.RegisterChangeCallback( OnConfigurationChanged, this );

        // We do not handle CancellationTokenRegistration.Dispose here.
        // The target is disposing: everything will be discarded, included
        // this instance of initializer.
        _target.StoppedToken.Register( () =>
        {
            _changeToken.Dispose();
            _loggerProvider._running = false;
        } );
    }

    [MemberNotNull( nameof( _target ), nameof( _loggerProvider ) )]
    void ApplyDynamicConfiguration( bool initialConfigMustWaitForApplication )
    {
        Debug.Assert( _section != null );
        // It has been Obsolete (Warn) for a long time. Now we throw. 
        if( _section["HandleAspNetLogs"] != null )
        {
            throw new CKException( "Configuration name \"HandleAspNetLogs\" is obsolete: please use \"HandleDotNetLogs\" (that defaults to true) instead." );
        }
        if( _section["LogUnhandledExceptions"] != null )
        {
            throw new CKException( "Configuration name \"LogUnhandledExceptions\" is not used anymore, it is now on the GrandOutput: use \"GrandOutput.TrackUnhandledExceptions\" instead (defaults to true)." );
        }

        bool dotNetLogs = !String.Equals( _section["HandleDotNetLogs"], "false", StringComparison.OrdinalIgnoreCase );

        LogFilter defaultFilter = LogFilter.Undefined;
        bool hasGlobalDefaultFilter = _section["GlobalDefaultFilter"] != null;
        bool errorParsingGlobalDefaultFilter = hasGlobalDefaultFilter && !LogFilter.TryParse( _section["GlobalDefaultFilter"]!, out defaultFilter );

        if( hasGlobalDefaultFilter && !errorParsingGlobalDefaultFilter )
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
                    Debug.Assert( _target != null, "Since !initialConfigMustWaitForApplication." );
                    defaultFilter = SetGlobalDefaultFilter( defaultFilter );
                }
            }
        }

        try
        {
            GrandOutputConfiguration c = CreateConfigurationFromSection( _section );
            if( _target == null )
            {
                Debug.Assert( _isDefaultGrandOutput && initialConfigMustWaitForApplication );
                _target = GrandOutput.EnsureActiveDefault( c );
                _loggerProvider = new GrandOutputLoggerAdapterProvider( _target );
            }
            else
            {
                Debug.Assert( _loggerProvider != null );
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
                _target = GrandOutput.EnsureActiveDefault();
                _loggerProvider = new GrandOutputLoggerAdapterProvider( _target );
            }
            _target.ExternalLog( Core.LogLevel.Fatal, message: $"While applying dynamic configuration.", ex: ex );
        }
        Debug.Assert( _loggerProvider != null );
        _loggerProvider._running = dotNetLogs;

        // Applying Tags.
        List<(CKTrait, LogClamper)>? parsedTags = null;
        foreach( var entry in _section.GetSection( "TagFilters" ).GetChildren() )
        {
            if( int.TryParse( entry.Key, out var idxEntry ) )
            {
                parsedTags = HandleTag( parsedTags, entry, entry["0"], entry["1"] );
            }
            else
            {
                parsedTags = HandleTag( parsedTags, entry, entry.Key, entry.Value );
            }
        }
        if( parsedTags != null )
        {
            ActivityMonitor.Tags.SetFilters( parsedTags.ToArray() );
        }
        if( hasGlobalDefaultFilter )
        {
            // Always log the parse error, but only log and applies if this is the initial configuration.
            if( errorParsingGlobalDefaultFilter )
            {
                _target.ExternalLog( Core.LogLevel.Error, message: $"Unable to parse configuration 'GlobalDefaultFilter'. Expected \"Debug\", \"Trace\", \"Verbose\", \"Monitor\", \"Terse\", \"Release\", \"Off\" or pairs of \"{{Group,Line}}\" levels where Group or Line can be Debug, Trace, Info, Warn, Error, Fatal or Off." );
            }
            else if( initialConfigMustWaitForApplication )
            {
                SetGlobalDefaultFilter( defaultFilter );
            }
        }

        List<(CKTrait, LogClamper)>? HandleTag( List<(CKTrait, LogClamper)>? parsedTags, IConfigurationSection entry, string? name, string? filter )
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
                        _target.ExternalLog( Core.LogLevel.Warn, message: $"Ignoring TagFilters '{entry.Path}': [{name},{filter}]. Tag is empty" );
                    else _target.ExternalLog( Core.LogLevel.Warn, message: $"Ignoring TagFilters '{entry.Path}': [{name},{filter}]. Unable to parse clamp value. Expected a LogFilter (followed by an optional '!'): \"Debug\", \"Trace\", \"Verbose\", \"Monitor\", \"Terse\", \"Release\", \"Off\" or pairs of \"{{Group,Line}}\" levels where Group or Line can be Debug, Trace, Info, Warn, Error, Fatal or Off." );
                }
            }
            return parsedTags;
        }
    }

    LogFilter SetGlobalDefaultFilter( LogFilter defaultFilter )
    {
        Debug.Assert( _target != null );
        if( defaultFilter.Group == LogLevelFilter.None || defaultFilter.Line == LogLevelFilter.None )
        {
            _target.ExternalLog( Core.LogLevel.Error, message: $"Invalid GlobalDefaultFilter = '{defaultFilter}'. using default 'Trace'." );
            defaultFilter = LogFilter.Trace;
        }
        _target.ExternalLog( Core.LogLevel.Info, message: $"Configuring ActivityMonitor.DefaultFilter to GlobalDefaultFilter = '{defaultFilter}'." );
        ActivityMonitor.DefaultFilter = defaultFilter;
        return defaultFilter;
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
        Throw.DebugAssert( obj is GrandOutputConfigurationInitializer );
        var initializer = (GrandOutputConfigurationInitializer)obj;
        initializer.ApplyDynamicConfiguration( false );
        initializer.RenewChangeToken();
    }

    void RenewChangeToken()
    {
        Debug.Assert( _changeToken != null && _section != null );
        // Disposes the previous change token.
        _changeToken.Dispose();
        // Reacquires the token: using this as the state keeps this object alive.
        var reloadToken = _section.GetReloadToken();
        _changeToken = reloadToken.RegisterChangeCallback( OnConfigurationChanged, this );
    }
}
