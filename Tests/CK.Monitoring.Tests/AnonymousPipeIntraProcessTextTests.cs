using NUnit.Framework;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace CK.Monitoring.Tests;

// This fails with NUnit 2.6.4 GUI runner:
// A SafeHandle destructor raises an exception during Garbage Collection.
// This reproduces the way the SimpleLogPipeReceiver works to show the bug
// with minimal complexity. 
[TestFixture]
public class AnonymousPipeIntraProcessTextTests
{
    [Test]
    public void sending_log_from_client()
    {
        using( var r = LogReceiver.Start() )
        {
            RunClient( r.PipeName );
            Assert.That( r.WaitEnd() == LogReceiverEndStatus.Normal );
        }
    }

    class PipeLogSenderClient : IDisposable
    {
        readonly StreamWriter _writer;
        readonly AnonymousPipeClientStream _client;

        public PipeLogSenderClient( string pipeHandlerName )
        {
            _client = new AnonymousPipeClientStream( PipeDirection.Out, pipeHandlerName );
            _writer = new StreamWriter( _client );
            _writer.WriteLine( "HELLO" );
        }

        public void Dispose()
        {
            _writer.WriteLine( "GOODBYE" );
            if( OperatingSystem.IsWindows() )
            {
                _client.WaitForPipeDrain();
            }
            _writer.Dispose();
            _client.Dispose();
        }

        public void SendText( string msg )
        {
            _writer.WriteLine( msg );
        }

    }

    public enum LogReceiverEndStatus
    {
        None,
        Normal,
        MissingEndMarker,
        Error
    }

    interface ILogReceiver : IDisposable
    {
        string PipeName { get; }

        LogReceiverEndStatus WaitEnd();
    }

    class LogReceiver : ILogReceiver
    {
        readonly AnonymousPipeServerStream _server;
        readonly StreamReader _reader;
        readonly Thread _thread;
        LogReceiverEndStatus _endFlag;

        LogReceiver()
        {
            _server = new AnonymousPipeServerStream( PipeDirection.In );
            _reader = new StreamReader( _server );
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

        public void Dispose()
        {
            _thread.Join();
            _server.Dispose();
            _reader.Dispose();
        }

        void Run( object? unused )
        {
            try
            {
                string? hello = _reader.ReadLine();
                Assert.That( hello == "HELLO" );
                int iLine = 0;
                for(; ; )
                {
                    var line = _reader.ReadLine();
                    if( line == null )
                    {
                        _endFlag = LogReceiverEndStatus.MissingEndMarker;
                        break;
                    }
                    if( line == "GOODBYE" )
                    {
                        _endFlag = LogReceiverEndStatus.Normal;
                        break;
                    }
                    Assert.That( line == $"Line n°{iLine++}" );
                }
                Assert.That( iLine == 20 );
            }
            catch( Exception ex )
            {
                _endFlag = LogReceiverEndStatus.Error;
                Assert.That( ex == null, "There should not be any error here." );
            }
        }

        public static ILogReceiver Start() => new LogReceiver();

    }

    void RunClient( string pipeHandlerName )
    {
        using( var pipe = new PipeLogSenderClient( pipeHandlerName ) )
        {
            for( int i = 0; i < 20; ++i )
            {
                Thread.Sleep( 100 );
                pipe.SendText( $"Line n°{i}" );
            }
        }
    }
}
