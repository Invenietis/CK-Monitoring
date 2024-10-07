using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Monitoring.Handlers;

/// <summary>
/// Base class (template method) that handles log buffering until a condition is satisfied and
/// handles <see cref="ISender"/> transient connection issues.
/// By default this condition is <see cref="CoreApplicationIdentity.IsInitialized"/> but this can be
/// overridden.
/// </summary>
/// <typeparam name="TConfiguration">This configuration type.</typeparam>
public abstract class BaseLogSender<TConfiguration> : IGrandOutputHandler where TConfiguration : IBaseLogSenderConfiguration
{
    TConfiguration _config;
    readonly FIFOBuffer<InputLogEntry> _buffer;
    ISender? _sender;
    int _lostEntries;
    bool _firstSuccess;

    /// <summary>
    /// Implementation of the actual log sender created by the <see cref="CreateSenderAsync(IActivityMonitor)"/>
    /// factory method. 
    /// </summary>
    protected interface ISender : IAsyncDisposable
    {
        /// <summary>
        /// Gets whether this target can send logs.
        /// </summary>
        bool IsActuallyConnected { get; }

        /// <summary>
        /// Tries to send a log entry.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="logEvent">The log entry to send.</param>
        /// <returns>True on success, false on failure.</returns>
        ValueTask<bool> TrySendAsync( IActivityMonitor monitor, IFullLogEntry logEvent );
    }

    /// <summary>
    /// Base constructor.
    /// </summary>
    /// <param name="c">The initial configuration.</param>
    protected BaseLogSender( TConfiguration c )
    {
        _config = c;
        _buffer = new FIFOBuffer<InputLogEntry>( c.InitialBufferSize );
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    protected TConfiguration Configuration => _config;

    /// <summary>
    /// Gets the sender if it has been created.
    /// </summary>
    protected ISender? Sender => _sender;

    /// <summary>
    /// Gets whether <see cref="CreateSenderAsync(IActivityMonitor)"/> can be called.
    /// Defaults to <see cref="CoreApplicationIdentity.IsInitialized"/>.
    /// </summary>
    protected virtual bool SenderCanBeCreated => CoreApplicationIdentity.IsInitialized;

    /// <summary>
    /// Activates this handler.
    /// If <see cref="SenderCanBeCreated"/> is true, <see cref="CreateSenderAsync(IActivityMonitor)"/>
    /// is called.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>True on success, false if the handler cannot be activated (it will be removed from the sink's handler list).</returns>
    public async virtual ValueTask<bool> ActivateAsync( IActivityMonitor monitor )
    {
        Throw.CheckOutOfRangeArgument( Configuration.InitialBufferSize >= 0 && Configuration.LostBufferSize >= 0 );
        if( SenderCanBeCreated )
        {
            _sender = await CreateSenderAsync( monitor );
            if( _sender == null )
            {
                // The sender cannot be created (not a transient error):
                // rejects the activation, this handler will be removed.
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Factory method of the <see cref="ISender"/> that sends logs.
    /// The sender must be logically connected, even if <see cref="ISender.IsActuallyConnected"/> is false.
    /// Null must be returned only for persistent errors: if the current <see cref="Configuration"/> is invalid
    /// (error logs should be emitted).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The sender or null if the configuration is invalid.</returns>
    protected abstract Task<ISender?> CreateSenderAsync( IActivityMonitor monitor );

    /// <inheritdoc />
    /// <remarks>
    /// When the configuration applies to this handler, <see cref="UpdateConfiguration(IActivityMonitor, TConfiguration)"/>
    /// must be called.
    /// </remarks>
    public abstract ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor monitor, IHandlerConfiguration c );

    /// <summary>
    /// Updates the size of the buffer and the <see cref="Configuration"/> to reference the new one.
    /// Must be called by <see cref="ApplyConfigurationAsync(IActivityMonitor, IHandlerConfiguration)"/> implementation
    /// when a configuration is for this handler.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="c">The new configuration.</param>
    protected void UpdateConfiguration( IActivityMonitor monitor, TConfiguration c )
    {
        int newC = _firstSuccess ? c.LostBufferSize : c.InitialBufferSize;
        if( _buffer.Capacity != newC )
        {
            monitor.Info( $"Resizing buffer from '{_buffer.Capacity}' to '{newC}'." );
            while( newC < _buffer.Count )
            {
                ++_lostEntries;
                _buffer.Pop().Release();
            }
            _buffer.Capacity = newC;
        }
        _config = c;
    }

    /// <summary>
    /// Sends the log entry: creates the sender if necessary and manages the buffer.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="logEvent">The log entry to send.</param>
    /// <returns>The awaitable.</returns>
    /// <exception cref="CKException">
    /// If the sender has not been created yet and the application identity is ready
    /// but CreateSenderAsync returned null: the exception will remove this handler from the sink's handler list.
    /// </exception>
    public virtual async ValueTask HandleAsync( IActivityMonitor monitor, InputLogEntry logEvent )
    {
        if( _sender == null && SenderCanBeCreated )
        {
            _sender = await CreateSenderAsync( monitor );
            if( _sender == null )
            {
                Throw.CKException( "Unable to create the sender." );
            }
        }
        if( _sender != null )
        {
            if( _buffer.Count > 0 )
            {
                while( _buffer.Count > 0 )
                {
                    if( !_sender.IsActuallyConnected || !await _sender.TrySendAsync( monitor, _buffer.Peek() ) )
                    {
                        PushEntry( logEvent );
                        return;
                    }
                    _buffer.Pop().Release();
                }
            }
            if( !_sender.IsActuallyConnected || !await _sender.TrySendAsync( monitor, logEvent ) )
            {
                PushEntry( logEvent );
            }
            else
            {
                if( _lostEntries > 0 )
                {
                    monitor.Error( $"{_lostEntries} log entries failed to be sent (current Capacity is {_buffer.Capacity})." );
                    _lostEntries = 0;
                }
                if( !_firstSuccess )
                {
                    _firstSuccess = true;
                    if( _buffer.Capacity != _config.LostBufferSize )
                    {
                        monitor.Info( $"Successfully sent the first logs: resizing buffer from '{_buffer.Capacity}' to '{_config.LostBufferSize}'." );
                        _buffer.Capacity = _config.LostBufferSize;
                    }
                }
            }
        }
        else
        {
            PushEntry( logEvent );
        }
    }

    private void PushEntry( InputLogEntry logEvent )
    {
        if( _buffer.IsFull )
        {
            ++_lostEntries;
            _buffer.Pop().Release();
        }
        logEvent.AddRef();
        _buffer.Push( logEvent );
    }

    /// <summary>
    /// By default, dispose the sender if it has been created.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>The awaitable.</returns>
    public virtual async ValueTask DeactivateAsync( IActivityMonitor monitor )
    {
        while( _buffer.Count > 0 ) _buffer.Pop().Release();
        if( _sender != null )
        {
            await _sender.DisposeAsync();
            _sender = null;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Does nothing at this level.
    /// Can be used to pool the connection if <see cref="ISender.IsActuallyConnected"/> is false.
    /// </remarks>
    public ValueTask OnTimerAsync( IActivityMonitor m, TimeSpan timerSpan ) => ValueTask.CompletedTask;

}
