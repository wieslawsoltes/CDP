using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Resiliency;

using System;
using System.IO;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Tests.Fixtures;

public class ProtocolResiliencyTests
{
    [AvaloniaFact]
    public async Task NegotiateAsync_MalformedTpktVersion_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.SendMalformedTpktVersion
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        await serverTask;
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_TruncatedHeader_ThrowsIOException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.SendTruncatedHeader
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        await serverTask;
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_ServerClosesConnection_ThrowsIOException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.CloseConnectionImmediately
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        await Assert.ThrowsAsync<IOException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", performSecurityHandshake: false, cancellationToken: ct));

        await serverTask;
    }
}
