using CK.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System;

namespace CK.Monitoring
{
    public sealed partial class InputLogEntry
    {
        /// <summary>
        /// Gets the current pool capacity. It starts at 200 and increases (with a warning) until <see cref="MaximalPoolCapacity"/>
        /// is reached (where errors are emitted). Warnings and errors are tagged with <see cref="InputLogPoolAlertTag"/>.
        /// </summary>
        public static int CurrentPoolCapacity => _currentCapacity;

        /// <summary>
        /// The current pool capacity increment until <see cref="CurrentPoolCapacity"/> reaches <see cref="MaximalPoolCapacity"/>.
        /// </summary>
        public const int PoolCapacityIncrement = 10;

        /// <summary>
        /// The maximal capacity. Once reached, newly acquired <see cref="ActivityMonitorExternalLogData"/> are garbage
        /// collected instead of returned to the pool.
        /// </summary>
        public static int MaximalPoolCapacity => _maximalCapacity;

        readonly static ConcurrentQueue<InputLogEntry> _items = new();
        static InputLogEntry? _fastItem;
        static int _numItems;
        static int _currentCapacity = 200;
        static int _maximalCapacity = 2000;
        static DateTime _nextPoolError;

        // For Log, OpenGroup and StaticLogger.
        internal static InputLogEntry AcquireInputLogEntry( string grandOutputId,
                                                            ref ActivityMonitorLogData data,
                                                            int groupDepth,
                                                            LogEntryType logType,
                                                            string monitorId,
                                                            LogEntryType previousEntryType,
                                                            DateTimeStamp previousLogTime )
        {
            InputLogEntry item = Aquire();
            item.Initialize( grandOutputId, ref data, groupDepth, logType, monitorId, previousEntryType, previousLogTime );
            return item;
        }

        // Only for monitor.CloseGroup().
        internal static InputLogEntry AcquireInputLogEntry( string grandOutputId,
                                                            DateTimeStamp closeLogTime,
                                                            IReadOnlyList<ActivityLogGroupConclusion> conclusions,
                                                            LogLevel logLevel,
                                                            int groupDepth,
                                                            string monitorId,
                                                            LogEntryType previousEntryType,
                                                            DateTimeStamp previousLogTime )
        {
            InputLogEntry item = Aquire();
            item.Initialize( grandOutputId, closeLogTime, conclusions, logLevel, groupDepth, monitorId, previousEntryType, previousLogTime );
            return item;
        }

        // For SinkLog.
        internal static InputLogEntry AcquireInputLogEntry( string grandOutputId,
                                                             string monitorId,
                                                             DateTimeStamp prevLogTime,
                                                             string text,
                                                             DateTimeStamp logTime,
                                                             LogLevel level,
                                                             CKTrait tags,
                                                             CKExceptionData? ex )
        {
            InputLogEntry item = Aquire();
            item.Initialize( grandOutputId, monitorId, prevLogTime, text, logTime, level, tags, ex );
            return item;
        }

        static InputLogEntry Aquire()
        {
            var item = _fastItem;
            if( item == null || Interlocked.CompareExchange( ref _fastItem, null, item ) != item )
            {
                if( _items.TryDequeue( out item ) )
                {
                    Interlocked.Decrement( ref _numItems );
                }
                else
                {
                    item = new InputLogEntry();
                }
            }
            return item;
        }

        static void Release( InputLogEntry c )
        {
            if( _fastItem != null || Interlocked.CompareExchange( ref _fastItem, c, null ) != null )
            {
                int poolCount = Interlocked.Increment( ref _numItems );
                // Strictly lower than to account for the _fastItem.
                if( poolCount < _currentCapacity )
                {
                    _items.Enqueue( c );
                    return;
                }
                // Current capacity is reached. Increasing it and emits a warning.
                // If the count reaches the MaximalCapacity, emits an error and don't increase the
                // limit anymore: log data will be garbage collected. If this error persists, it indicates a leak somewhere!
                if( poolCount >= MaximalPoolCapacity )
                {
                    // Adjust the pool count.
                    Interlocked.Decrement( ref _numItems );
                    // Signals the error continuously once per second.
                    var now = DateTime.UtcNow;
                    if( _nextPoolError < now )
                    {
                        _nextPoolError = now.AddSeconds( 1 );
                        ActivityMonitor.StaticLogger.UnfilteredLog( LogLevel.Error,
                                                                    GrandOutput.InputLogPoolAlertTag,
                                                                    $"The log data pool reached its maximal capacity of {MaximalPoolCapacity}. This may indicate a peak of activity " +
                                                                    $"or a leak (missing ActivityMonitorExternalLogData.Release() calls) if this error persists.", null );
                    }
                }
                else
                {
                    int newCapacity = Interlocked.Add( ref _currentCapacity, PoolCapacityIncrement );
                    ActivityMonitor.StaticLogger.UnfilteredLog( LogLevel.Warn, GrandOutput.InputLogPoolAlertTag, $"The log data pool has been increased to {newCapacity}.", null );
                }
            }
        }

    }

}

