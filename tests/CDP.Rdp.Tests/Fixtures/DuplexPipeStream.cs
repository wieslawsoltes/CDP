namespace CDP.Rdp.Tests.Fixtures;

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A bidirectional stream wrapping a PipeReader for reading and a PipeWriter for writing.
/// </summary>
public sealed class DuplexPipeStream : Stream
{
    private readonly PipeReader _reader;
    private readonly PipeWriter _writer;
    private bool _isDisposed;

    public DuplexPipeStream(PipeReader reader, PipeWriter writer)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public override bool CanRead => !_isDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => !_isDisposed;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _writer.FlushAsync().GetAwaiter().GetResult();
    public override Task FlushAsync(CancellationToken cancellationToken) => _writer.FlushAsync(cancellationToken).AsTask();

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ReadResult result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        ReadOnlySequence<byte> sequence = result.Buffer;

        if (sequence.IsEmpty && result.IsCompleted)
            return 0;

        int bytesToCopy = (int)Math.Min(count, sequence.Length);
        ReadOnlySequence<byte> slice = sequence.Slice(0, bytesToCopy);
        slice.CopyTo(buffer.AsSpan(offset, bytesToCopy));

        _reader.AdvanceTo(slice.End);
        return bytesToCopy;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ReadResult result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        ReadOnlySequence<byte> sequence = result.Buffer;

        if (sequence.IsEmpty && result.IsCompleted)
            return 0;

        int bytesToCopy = (int)Math.Min(buffer.Length, sequence.Length);
        ReadOnlySequence<byte> slice = sequence.Slice(0, bytesToCopy);
        slice.CopyTo(buffer.Span.Slice(0, bytesToCopy));

        _reader.AdvanceTo(slice.End);
        return bytesToCopy;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _writer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _writer.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _writer.Complete();
            _reader.Complete();
        }
        base.Dispose(disposing);
    }
}
