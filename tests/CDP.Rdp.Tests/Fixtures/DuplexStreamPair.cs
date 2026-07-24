namespace CDP.Rdp.Tests.Fixtures;

using System;
using System.IO;
using System.IO.Pipelines;

/// <summary>
/// Provides a bidirectionally connected stream pair simulating a network socket connection.
/// </summary>
public sealed class DuplexStreamPair : IDisposable
{
    public Stream ClientStream { get; }
    public Stream ServerStream { get; }

    public DuplexStreamPair(int bufferSize = 65536)
    {
        PipeOptions options = new PipeOptions(
            pauseWriterThreshold: bufferSize,
            resumeWriterThreshold: bufferSize / 2,
            minimumSegmentSize: 4096,
            useSynchronizationContext: false);

        Pipe pipeA = new Pipe(options);
        Pipe pipeB = new Pipe(options);

        ClientStream = new DuplexPipeStream(pipeB.Reader, pipeA.Writer);
        ServerStream = new DuplexPipeStream(pipeA.Reader, pipeB.Writer);
    }

    public void Dispose()
    {
        ClientStream.Dispose();
        ServerStream.Dispose();
    }
}
