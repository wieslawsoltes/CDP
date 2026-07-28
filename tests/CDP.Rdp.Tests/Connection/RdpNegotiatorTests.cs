using Avalonia.Headless.XUnit;
namespace CDP.Rdp.Tests.Connection;

using System;
using System.Threading.Tasks;
using CDP.Rdp.Exceptions;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

public class RdpNegotiatorTests
{
    [AvaloniaFact]
    public async Task NegotiateAsync_ServerAcceptsSSL_ReturnsTlsTransport()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.AcceptRequestedProtocol,
            ResponseFlags = 0x01
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            pair.ClientStream,
            "localhost",
            RdpSecurityProtocol.Ssl,
            performSecurityHandshake: false,
            cancellationToken: ct);

        await serverTask;

        Assert.NotNull(transport);
        Assert.Equal(RdpSecurityProtocol.Ssl, transport.Protocol);
        Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
        Assert.Equal(RdpSecurityProtocol.Ssl, negotiator.SelectedProtocol);
        Assert.Equal(0x01, negotiator.ResponseFlags);
        Assert.NotNull(server.ReceivedRequest);
        Assert.Equal(RdpSecurityProtocol.Ssl, server.ReceivedRequest.Value.RequestedProtocols);
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_ServerAcceptsPlainRdp_ReturnsPlainTransport()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.ForceProtocol,
            ForcedProtocol = RdpSecurityProtocol.Rdp
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            pair.ClientStream,
            "localhost",
            RdpSecurityProtocol.Rdp,
            performSecurityHandshake: false,
            cancellationToken: ct);

        await serverTask;

        Assert.NotNull(transport);
        Assert.Equal(RdpSecurityProtocol.Rdp, transport.Protocol);
        Assert.False(transport.IsEncrypted);
        Assert.Equal(RdpNegotiationState.Connected, negotiator.State);
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_ServerRejectsWithFailure_ThrowsRdpNegotiationException()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.RejectWithFailure,
            FailureCode = RdpNegotiationFailureCode.HybridRequiredByServer
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        RdpNegotiationException ex = await Assert.ThrowsAsync<RdpNegotiationException>(() =>
            negotiator.NegotiateAsync(pair.ClientStream, "localhost", RdpSecurityProtocol.Ssl, performSecurityHandshake: false, cancellationToken: ct));

        await serverTask;

        Assert.Equal(RdpNegotiationFailureCode.HybridRequiredByServer, ex.FailureCode);
        Assert.Equal(RdpNegotiationState.Failed, negotiator.State);
    }

    [AvaloniaFact]
    public async Task NegotiateAsync_RoutingCookieProvided_EncodesInConnectionRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        SimulatedRdpServer server = new SimulatedRdpServer(pair.ServerStream)
        {
            Behavior = ServerResponseBehavior.AcceptRequestedProtocol
        };

        Task serverTask = server.ProcessConnectionRequestAsync(ct);

        RdpNegotiator negotiator = new RdpNegotiator();
        IRdpSecurityTransport transport = await negotiator.NegotiateAsync(
            pair.ClientStream,
            "localhost",
            RdpSecurityProtocol.Ssl,
            routingCookie: "testuser",
            performSecurityHandshake: false,
            cancellationToken: ct);

        await serverTask;

        Assert.NotNull(transport);
        Assert.NotNull(server.ReceivedX224Header);
        Assert.True(server.ReceivedX224Header.Value.LengthIndicator > 14);
    }
}
