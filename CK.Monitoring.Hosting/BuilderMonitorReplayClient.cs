using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Monitoring.Hosting
{
    sealed class BuilderMonitorReplayClient : IActivityMonitorClient
    {
        readonly List<object> _logs = new List<object>();

        public void OnAutoTagsChanged( CKTrait newTrait ) { }
        public void OnGroupClosing( IActivityLogGroup group, ref List<ActivityLogGroupConclusion>? conclusions ) { }
        public void OnTopicChanged( string newTopic, string? fileName, int lineNumber ) { }

        public void OnGroupClosed( IActivityLogGroup group, IReadOnlyList<ActivityLogGroupConclusion> conclusions )
        {
            _logs.Add( Tuple.Create( group.CloseLogTime, conclusions ) );
        }

        public void OnOpenGroup( IActivityLogGroup group )
        {
            _logs.Add( Tuple.Create( group.Data.AcquireExternalData() ) );
        }

        public void OnUnfilteredLog( ref ActivityMonitorLogData data )
        {
            _logs.Add( data.AcquireExternalData() );
        }

        public void Replay( IActivityMonitor target )
        {
            foreach( var log in _logs )
            {
                switch( log )
                {
                    case ActivityMonitorExternalLogData line:
                        var dLine = new ActivityMonitorLogData( line );
                        target.UnfilteredLog( ref dLine );
                        line.Release();
                        break;
                    case Tuple<ActivityMonitorExternalLogData> group:
                        var dGroup = new ActivityMonitorLogData( group.Item1 );
                        target.UnfilteredOpenGroup( ref dGroup );
                        group.Item1.Release();
                        break;
                    case Tuple<DateTimeStamp,IReadOnlyList<ActivityLogGroupConclusion>> close:
                        target.CloseGroup( close.Item2, close.Item1 );
                        break;
                }
            }
        }
    }
}
