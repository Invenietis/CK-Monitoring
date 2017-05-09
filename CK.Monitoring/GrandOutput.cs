using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CK.Core;
using CK.Monitoring.Impl;
using System.Linq;
using System.Reflection;

namespace CK.Monitoring
{
    /// <summary>
    /// A GrandOutput collects activity of multiple <see cref="IActivityMonitor"/>. 
    /// It is usually useless to explicitly create an instance of GrandOutput: the <see cref="Default"/> one is 
    /// available as soon as <see cref="EnsureActiveDefault"/> is called 
    /// and will be automatically used by new <see cref="ActivityMonitor"/>.
    /// </summary>
    public sealed partial class GrandOutput : IDisposable
    {
        readonly List<WeakReference<GrandOutputClient>> _clients;
        readonly DispatcherSink _sink;

        static GrandOutput _default;
        static readonly object _defaultLock = new object();

        /// <summary>
        /// Gets the default <see cref="GrandOutput"/> for the current Application Domain.
        /// Note that <see cref="EnsureActiveDefault"/> must have been called, otherwise this static property is null.
        /// </summary>
        public static GrandOutput Default => _default;

        /// <summary>
        /// Ensures that the <see cref="Default"/> GrandOutput is created and that any <see cref="ActivityMonitor"/> that will be created in this
        /// application domain will automatically have a <see cref="GrandOutputClient"/> registered for this Default GrandOutput.
        /// </summary>
        /// <param name="configuration">
        /// Configuration to apply to the default GrandOutput.
        /// When null, a default configuration with a <see cref="Handlers.TextFileConfiguration"/> in a "Text" path is configured.
        /// </param>
        /// <returns>The Default GrandOutput.</returns>
        /// <remarks>
        /// This method is thread-safe (a simple lock protects it) and uses a <see cref="ActivityMonitor.AutoConfiguration"/> action 
        /// that uses <see cref="EnsureGrandOutputClient(IActivityMonitor)"/> on newly created ActivityMonitor.
        /// </remarks>
        static public GrandOutput EnsureActiveDefault( GrandOutputConfiguration configuration )
        {
            lock( _defaultLock )
            {
                if (_default == null)
                {
                    SystemActivityMonitor.EnsureStaticInitialization();
                    if (configuration == null)
                    {
                        configuration = new GrandOutputConfiguration()
                                            .AddHandler( new Handlers.TextFileConfiguration() { Path = "Text" });
                        configuration.InternalClone = true;
                    }
                    _default = new GrandOutput(configuration);
                    ActivityMonitor.AutoConfiguration += m => Default.EnsureGrandOutputClient(m);
                }
                else if(configuration != null) _default.ApplyConfiguration(configuration);
            }
            return _default;
        }

        /// <summary>
        /// Applies a configuration.
        /// </summary>
        /// <param name="configuration">The configuration to apply.</param>
        public void ApplyConfiguration(GrandOutputConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (!configuration.InternalClone)
            {
                configuration = configuration.Clone();
                configuration.InternalClone = true;
            }
            _sink.ApplyConfiguration(configuration);
        }

        /// <summary>
        /// Settable factory method for <see cref="IGrandOutputHandler"/>.
        /// Default implementation relies on Handlers that must be in the same 
        /// assembly and namespace as their configuration objects and named the 
        /// same without the "Configuration" suffix.
        /// </summary>
        static public Func<IHandlerConfiguration,IGrandOutputHandler> CreateHandler = config =>
        {
            string name = config.GetType().GetTypeInfo().FullName;
            if (!name.EndsWith("Configuration")) throw new CKException($"Configuration handler type name must end with 'Configuration': {name}.");
            name = config.GetType().AssemblyQualifiedName.Replace("Configuration,", ",");
            Type t = Type.GetType(name, throwOnError: true);
            return (IGrandOutputHandler)Activator.CreateInstance(t, new[] { config });
        };

        /// <summary>
        /// Initializes a new <see cref="GrandOutput"/>. 
        /// </summary>
        public GrandOutput( GrandOutputConfiguration config )
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _clients = new List<WeakReference<GrandOutputClient>>();
            _sink = new DispatcherSink(config.TimerDuration, TimeSpan.FromMinutes(5), DoGarbageDeadClients );
            ApplyConfiguration(config);
        }

        /// <summary>
        /// Ensures that a client for this GrandOutput is registered on a monitor.
        /// </summary>
        /// <param name="monitor">The monitor onto which a <see cref="GrandOutputClient"/> must be registered.</param>
        /// <returns>A newly created client or the already existing one.</returns>
        public GrandOutputClient EnsureGrandOutputClient( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor" );
            Func<GrandOutputClient> reg = () =>
                {
                    var c = new GrandOutputClient( this );
                    lock( _clients ) _clients.Add( new WeakReference<GrandOutputClient>( c ) ); 
                    return c;
                };
            return monitor.Output.RegisterUniqueClient( b => b.Central == this, reg );
        }

        /// <summary>
        /// Gets the sink.
        /// </summary>
        public IGrandOutputSink Sink => _sink;

        void DoGarbageDeadClients()
        {
            lock (_clients)
            {
                int count = 0;
                for (int i = 0; i < _clients.Count; ++i)
                {
                    GrandOutputClient cw;
                    if (!_clients[i].TryGetTarget(out cw) || !cw.IsBoundToMonitor)
                    {
                        _clients.RemoveAt(i--);
                        ++count;
                    }
                }
            }
        }


        /// <summary>
        /// Gets whether this GrandOutput has been disposed.
        /// </summary>
        public bool IsDisposed => !_sink.IsRunning;

        /// <summary>
        /// Closes this <see cref="GrandOutput"/>.
        /// </summary>
        /// <param name="monitor">Monitor that will be used. Must not be null.</param>
        /// <param name="millisecondsBeforeForceClose">Maximal time to wait.</param>
        public void Dispose( IActivityMonitor monitor, int millisecondsBeforeForceClose = Timeout.Infinite )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof(monitor) );
            if(_sink.IsRunning)
            {
                _sink.Stop(millisecondsBeforeForceClose);
            }
        }

        /// <summary>
        /// Calls <see cref="Dispose(IActivityMonitor,int)"/> with a <see cref="SystemActivityMonitor"/> 
        /// and <see cref="Timeout.Infinite"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(new SystemActivityMonitor(applyAutoConfigurations: false, topic: null ), Timeout.Infinite );
        }


    }
}
