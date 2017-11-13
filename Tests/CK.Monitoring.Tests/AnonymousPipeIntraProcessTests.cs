using CK.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core.Impl;
using System.IO;
using CK.Monitoring;
using System.Diagnostics;
using System.Threading;
using FluentAssertions;

namespace CK.Monitoring.Tests
{
    // This fails with NUnit 2.6.4 GUI runner:
    // A SafeHandle destructor raises an exception during Garbage Collection.
#if !NET461
    [TestFixture]
    public class AnonymousPipeIntraProcessTests
    {
        class ActivityMonitorAnonymousPipeLogSenderClient : IActivityMonitorClient, IDisposable
        {
            readonly CKBinaryWriter _writer;
            readonly AnonymousPipeClientStream _client;

            public ActivityMonitorAnonymousPipeLogSenderClient( string pipeHandlerName )
            {
                _client = new AnonymousPipeClientStream( PipeDirection.Out, pipeHandlerName );
                _writer = new CKBinaryWriter( _client );
                _writer.Write( LogReader.CurrentStreamVersion );
            }

            public void Dispose()
            {
                _client.WriteByte( 0 );
                _client.WaitForPipeDrain();
                _writer.Dispose();
                _client.Dispose();
            }

            void IActivityMonitorClient.OnAutoTagsChanged( CKTrait newTrait )
            {
            }

            void IActivityMonitorClient.OnGroupClosed( IActivityLogGroup group, IReadOnlyList<ActivityLogGroupConclusion> conclusions )
            {
                LogEntry.WriteCloseGroup( _writer, group.GroupLevel, group.CloseLogTime, conclusions );
            }

            void IActivityMonitorClient.OnGroupClosing( IActivityLogGroup group, ref List<ActivityLogGroupConclusion> conclusions )
            {
            }

            void IActivityMonitorClient.OnOpenGroup( IActivityLogGroup group )
            {
                LogEntry.WriteLog( _writer, true, group.GroupLevel, group.LogTime, group.GroupText, group.GroupTags, group.ExceptionData, group.FileName, group.LineNumber );
            }

            void IActivityMonitorClient.OnTopicChanged( string newTopic, string fileName, int lineNumber )
            {
            }

            void IActivityMonitorClient.OnUnfilteredLog( ActivityMonitorLogData data )
            {
                LogEntry.WriteLog( _writer, false, data.Level, data.LogTime, data.Text, data.Tags, data.ExceptionData, data.FileName, data.LineNumber );
            }
        }

        [Test]
        public void sending_log_from_client()
        {
            var m = new ActivityMonitor();
            var txt = new StupidStringClient();
            m.Output.RegisterClient( txt );
            using( m.Output.CreateBridgeTo( TestHelper.ConsoleMonitor.Output.BridgeTarget ) )
            {
                using( var r = LogReceiver.Start( m, false ) )
                {
                    RunClient( r.PipeName );
                    r.WaitEnd().Should().Be( LogReceiverEndStatus.Normal );
                }
            }
            var logs = txt.ToString();
            logs.Should().Contain( "From client." )
                         .And.Contain( "An Exception for the fun." )
                         .And.Contain( "Info n°0" )
                         .And.Contain( "Info n°19" );
        }

        public enum LogReceiverEndStatus
        {
            None,
            Normal,
            MissingEndMarker,
            Error
        }


        class LogReceiver : IDisposable
        {
            readonly AnonymousPipeServerStream _server;
            readonly CKBinaryReader _reader;
            readonly IActivityMonitor _monitor;
            readonly Thread _thread;
            LogReceiverEndStatus _endFlag;

            LogReceiver( IActivityMonitor m, bool interProcess )
            {
                var inherit = interProcess ? HandleInheritability.Inheritable : HandleInheritability.None;
                _server = new AnonymousPipeServerStream( PipeDirection.In, inherit );
                _reader = new CKBinaryReader( _server );
                _monitor = m;
                PipeName = _server.GetClientHandleAsString();
                _thread = new Thread( Run );
                _thread.IsBackground = true;
                _thread.Start();
            }

            public string PipeName { get; }

            public LogReceiverEndStatus WaitEnd()
            {
                _thread.Join();
                return _endFlag;
            }

            public void OnProcessStarted()
            {
                _server.DisposeLocalCopyOfClientHandle();
            }

            public void Dispose()
            {
                _thread.Join();
                _reader.Dispose();
                _server.Dispose();
            }

            void Run()
            {
                try
                {
                    int streamVersion = _reader.ReadInt32();
                    for( ; ; )
                    {
                        var e = LogEntry.Read( _reader, streamVersion, out bool badEndOfStream );
                        if( e == null || badEndOfStream )
                        {
                            _endFlag = badEndOfStream ? LogReceiverEndStatus.MissingEndMarker : LogReceiverEndStatus.Normal;
                            break;
                        }
                        switch( e.LogType )
                        {
                            case LogEntryType.Line:
                                _monitor.UnfilteredLog( e.Tags, e.LogLevel, e.Text, e.LogTime, CKException.CreateFrom( e.Exception ), e.FileName, e.LineNumber );
                                break;
                            case LogEntryType.OpenGroup:
                                _monitor.UnfilteredOpenGroup( e.Tags, e.LogLevel, null, e.Text, e.LogTime, CKException.CreateFrom( e.Exception ), e.FileName, e.LineNumber );
                                break;
                            case LogEntryType.CloseGroup:
                                _monitor.CloseGroup( e.LogTime, e.Conclusions );
                                break;
                        }
                    }
                }
                catch( Exception ex )
                {
                    _endFlag = LogReceiverEndStatus.Error;
                    ActivityMonitor.CriticalErrorCollector.Add( ex, "While LogReceiver.Run." );
                }
            }

            public static LogReceiver Start( IActivityMonitor m, bool interProcess ) => new LogReceiver( m, interProcess );

        }

        void RunClient( string pipeHandlerName )
        {
            var m = new ActivityMonitor( false );
            using( var pipe = new ActivityMonitorAnonymousPipeLogSenderClient( pipeHandlerName ) )
            {
                m.Output.RegisterClient( pipe );
                using( m.OpenInfo( "From client." ) )
                {
                    m.Fatal( "A fatal.", new Exception( "An Exception for the fun." ) );
                    for( int i = 0; i < 20; ++i )
                    {
                        Thread.Sleep( 100 );
                        m.Info( $"Info n°{i}" );
                    }
                }
                m.Output.UnregisterClient( pipe );
            }
        }
    }
#endif
}
