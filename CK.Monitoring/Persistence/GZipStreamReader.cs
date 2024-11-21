using CK.Core;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace CK.Monitoring;

internal sealed class GZipStreamReader : Stream
{
    readonly GZipStream _stream;
    long _position;

    public GZipStreamReader( Stream stream )
    {
        _stream = new GZipStream( stream, CompressionMode.Decompress );
        Debug.Assert( _stream.CanSeek == false );
    }

    protected override void Dispose( bool disposing )
    {
        if( disposing ) _stream.Dispose();
        base.Dispose( disposing );
    }

    public override int Read( Span<byte> buffer )
    {
        int read = _stream.Read( buffer );
        _position += read;
        return read;
    }

    public override int Read( byte[] array, int offset, int count ) => Read( array.AsSpan( offset, count ) );

    public override void Flush() => Throw.NotSupportedException();

    public override long Seek( long offset, SeekOrigin origin ) => Throw.NotSupportedException<long>();

    public override void SetLength( long value ) => Throw.NotSupportedException();

    public override void Write( byte[] array, int offset, int count ) => Throw.NotSupportedException();

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => Throw.NotSupportedException<long>();

    public override long Position
    {
        get { return _position; }
        set { Throw.NotSupportedException(); }
    }
}

