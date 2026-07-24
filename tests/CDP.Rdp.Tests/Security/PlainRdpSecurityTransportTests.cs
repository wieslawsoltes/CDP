namespace CDP.Rdp.Tests.Security;

using System.IO;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

public class PlainRdpSecurityTransportTests
{
    [Fact]
    public async Task HandshakeAsync_PlainTransport_CompletesWithoutError()
    {
        var ct = TestContext.Current.CancellationToken;
        using DuplexStreamPair pair = new DuplexStreamPair();
        await using PlainRdpSecurityTransport transport = new PlainRdpSecurityTransport(pair.ClientStream);

        Assert.Equal(RdpSecurityProtocol.Rdp, transport.Protocol);
        Assert.False(transport.IsEncrypted);
        Assert.Same(pair.ClientStream, transport.TransportStream);

        await transport.HandshakeAsync("localhost", ct);
    }
}
