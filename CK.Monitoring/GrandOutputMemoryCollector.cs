using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Monitoring
{
    /// <summary>
    /// A temporary <see cref="ILogEntry"/> collector created by <see cref="GrandOutput.CreateMemoryCollector(int,bool)"/>
    /// that must be disposed.
    /// <para>
    /// This is intended to be used in tests: there is little to no interest to
    /// use this elsewhere.
    /// </para>
    /// </summary>
    public sealed class GrandOutputMemoryCollector : Handlers.IDynamicGrandOutputHandler, IDisposable
    {
        readonly DispatcherSink _sink;
        readonly Handler _handler;
        // There won't be a lot of concurrent access: the handler.HandleAsync will
        // will take the lock directly, except when Extract/CopyTo methods
        // are called.
        // Instead of a ConcurrentQueue, we use a simple locked FIFOBuffer.
        readonly FIFOBuffer<ILogEntry> _buffer;
        int _disposed;

        internal GrandOutputMemoryCollector( DispatcherSink sink, int maxCapacity, bool ignoreCloseGroup )
        {
            _sink = sink;
            _handler = new Handler( this, ignoreCloseGroup );
            _buffer = new FIFOBuffer<ILogEntry>( Math.Max( 1 + maxCapacity/4, 512 ), maxCapacity );
            _sink.AddDynamicHandler( _handler );
        }

        /// <summary>
        /// Gets the current count of entries. This can change anytime.
        /// </summary>
        public int Count => _buffer.Count;

        /// <summary>
        /// Gets the maximal number of collected entries.
        /// This is a FIFO buffer (oldest entries are automatocally discarded).
        /// </summary>
        public int MaxCapacity => _buffer.MaxDynamicCapacity;

        /// <summary>
        /// Collects all the current <see cref="ILogEntry"/> and clears them.
        /// </summary>
        /// <returns>The current entries.</returns>
        public ILogEntry[] ExtractAllEntries()
        {
            lock( _buffer )
            {
                var a = _buffer.ToArray();
                _buffer.Clear();
                return a;
            }
        }

        /// <summary>
        /// Collects the current texts and clears the current entries.
        /// </summary>
        /// <returns>The current log texts.</returns>
        public ImmutableArray<string> ExtractAllTexts()
        {
            var a = ExtractAllEntries();
            var b = ImmutableArray.CreateBuilder<string>( a.Length );
            bool hasCloseGroup = false;
            foreach( var e in a )
            {
                if( e.Text == null ) hasCloseGroup = true;
                else b.Add( e.Text );
            }
            return hasCloseGroup ? b.ToImmutableArray() : b.MoveToImmutable();
        }

        /// <summary>
        /// Copies the latest entries, optionally clears all the current entries
        /// and returns the number of entries copied into <paramref name="newest"/>.
        /// </summary>
        /// <param name="newest">A target destination.</param>
        /// <param name="clearAll">True to clear all the current entries.</param>
        /// <returns>
        /// The number of entries copied in newest. <see cref="Span{T}.Slice(int, int)"/> should be used.
        /// </returns>
        public int CopyTo( Span<ILogEntry> newest, bool clearAll = false )
        {
            lock( _buffer )
            {
                int c = _buffer.CopyTo( newest );
                if( clearAll ) _buffer.Clear();
                return c;
            }
        }

        /// <summary>
        /// Gets whether this handler is disposed.
        /// Note that the entries are not cleared when the collector is disposed.
        /// </summary>
        public bool IsDisposed => _disposed != 0;

        IGrandOutputHandler Handlers.IDynamicGrandOutputHandler.Handler => _handler;

        /// <summary>
        /// Dispose this handler. No more entries will be collected but the
        /// recevied entries are kept.
        /// </summary>
        public void Dispose()
        {
            if( Interlocked.Exchange( ref _disposed, 1) == 0 )
            {
                _sink.RemoveDynamicHandler( this );
            }
        }

        sealed class Handler : IGrandOutputHandler
        {
            readonly GrandOutputMemoryCollector _h;
            readonly bool _ignoreCloseGroup;

            public Handler( GrandOutputMemoryCollector h, bool ignoreCloseGroup )
            {
                _h = h;
                _ignoreCloseGroup = ignoreCloseGroup;
            }

            public ValueTask<bool> ActivateAsync( IActivityMonitor monitor ) => ValueTask.FromResult( true );

            public ValueTask<bool> ApplyConfigurationAsync( IActivityMonitor monitor, IHandlerConfiguration c ) => ValueTask.FromResult( true );

            public ValueTask DeactivateAsync( IActivityMonitor monitor )
            {
                _h.Dispose();
                return default;
            }

            public ValueTask HandleAsync( IActivityMonitor monitor, InputLogEntry logEvent ) 
            {
                // No race condition here. The buffer is always here and if we add a new
                // entry in it when it is disposed, we don't care.
                if( _h._disposed == 0 && (!_ignoreCloseGroup || logEvent.LogType != LogEntryType.CloseGroup) )
                {
                    var e = logEvent.CreateSimpleLogEntry();
                    var b = _h._buffer;
                    lock( b )
                    {
                        b.Push( e );
                    }
                }
                return default;
            }

            public ValueTask OnTimerAsync( IActivityMonitor monitor, TimeSpan timerSpan ) => default;
        }

    }

}
