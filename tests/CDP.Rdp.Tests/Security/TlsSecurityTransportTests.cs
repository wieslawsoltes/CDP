namespace CDP.Rdp.Tests.Security;

using System;
using System.IO;
using System.Threading.Tasks;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;
using CDP.Rdp.Tests.Fixtures;

public class TlsSecurityTransportTests
{
    [Fact]
    public void Properties_BeforeHandshake_ReflectsExpectedValues()
    {
        using DuplexStreamPair pair = new DuplexStreamPair();
        using TlsSecurityTransport transport = new TlsSecurityTransport(pair.ClientStream);

        Assert.Equal(RdpSecurityProtocol.Ssl, transport.Protocol);
        Assert.False(transport.IsEncrypted);
    }
}
