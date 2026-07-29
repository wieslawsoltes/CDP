using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CDP.Rdp.Tests")]

namespace CDP.Rdp.Session;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CDP.Rdp.Channels;
using CDP.Rdp.Frames;
using CDP.Rdp.Input;
using CDP.Rdp.Protocol;
using CDP.Rdp.Security;

/// <summary>
/// Primary RDP client session implementation managing connection state lifecycle,
/// background frame processing, and input event dispatching.
/// </summary>
public sealed class RdpClient : IRdpSession
{
    private const int MaxFastPathPacketLength = 16384;
    private const int MaxTpktPacketLength = 32768;

    private readonly RdpSessionOptions _options;
    private readonly Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>>? _transportFactory;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly RdpFastPathFrameAssembler _fastPathAssembler = new();
    private RdpConnectionState _state = RdpConnectionState.Disconnected;
    private IRdpSecurityTransport? _transport;
    private Stream? _networkStream;
    private TcpClient? _tcpClient;
    private StaticVirtualChannelManager? _svcManager;
    private DynamicVirtualChannelManager? _dvcManager;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;
    private ulong _frameIdCounter;
    private ushort _userId;
    private ushort _ioChannelId;
    private uint _shareId;
    private int _isDisposed;

    public RdpConnectionState State => _state;
    public RdpSessionOptions Options => _options;
    public StaticVirtualChannelManager? StaticVirtualChannels => _svcManager;
    public DynamicVirtualChannelManager? DynamicVirtualChannels => _dvcManager;

    public event EventHandler<RdpFrameUpdateEventArgs>? FrameUpdated;
    public event EventHandler<RdpConnectionStateChangedEventArgs>? StateChanged;

    public RdpClient(RdpSessionOptions options)
        : this(options, null)
    {
    }

    public RdpClient(
        RdpSessionOptions options,
        Func<RdpSessionOptions, CancellationToken, Task<IRdpSecurityTransport>>? transportFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transportFactory = transportFactory;
    }

    private void SetState(RdpConnectionState newState, Exception? exception = null)
    {
        RdpConnectionState oldState = _state;
        if (oldState == newState) return;

        _state = newState;
        StateChanged?.Invoke(this, new RdpConnectionStateChangedEventArgs(oldState, newState, exception));
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            throw new ObjectDisposedException(nameof(RdpClient));

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw new ObjectDisposedException(nameof(RdpClient));
        }
        try
        {
            if (_state != RdpConnectionState.Disconnected)
                throw new InvalidOperationException($"Cannot connect from state: {_state}");

            try
            {
                SetState(RdpConnectionState.Connecting);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.ConnectTimeout);

                if (_transportFactory != null)
                {
                    SetState(RdpConnectionState.Negotiating);
                    _transport = await _transportFactory(_options, timeoutCts.Token).ConfigureAwait(false);
                }
                else
                {
                    _tcpClient = new TcpClient();
                    await _tcpClient.ConnectAsync(_options.Host, _options.Port, timeoutCts.Token).ConfigureAwait(false);
                    _networkStream = _tcpClient.GetStream();

                    SetState(RdpConnectionState.Negotiating);

                    var negotiator = new RdpNegotiator();
                    System.Net.Security.RemoteCertificateValidationCallback? certCallback = _options.ServerCertificateValidationCallback;
                    if (certCallback == null && _options.AcceptUntrustedCertificates)
                    {
                        certCallback = (sender, cert, chain, errors) => true;
                    }

                    _transport = await negotiator.NegotiateAsync(
                        _networkStream,
                        _options.Host,
                        _options.RequestedProtocols,
                        username: _options.Username,
                        password: _options.Password,
                        domain: _options.Domain,
                        performSecurityHandshake: true,
                        certValidation: certCallback,
                        cancellationToken: timeoutCts.Token).ConfigureAwait(false);
                }

                SetState(RdpConnectionState.Authenticating);

                _svcManager = new StaticVirtualChannelManager();
                _dvcManager = new DynamicVirtualChannelManager();

                if (_transportFactory == null)
                {
                    SetState(RdpConnectionState.Activating);
                    var activation = new RdpActivationSequence(_transport, _options);
                    RdpActivationResult result = await activation.ActivateAsync(timeoutCts.Token).ConfigureAwait(false);
                    _userId = result.UserId;
                    _ioChannelId = result.IoChannelId;
                    _shareId = result.ShareId;
                }
                else
                {
                    // Injected transports are used by deterministic protocol tests and
                    // represent an already activated connection.
                    _userId = 1002;
                    _ioChannelId = 1003;
                    _shareId = 0x000103EA;
                }

                SetState(RdpConnectionState.Connected);

                _cts = new CancellationTokenSource();
                _receiveLoopTask = Task.Run(() => ProcessingLoopAsync(_cts.Token), CancellationToken.None);
            }
            catch (Exception ex)
            {
                SetState(RdpConnectionState.Faulted, ex);
                await CleanupAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task ProcessingLoopAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[65536];
        int bytesInBuffer = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && _transport != null)
            {
                int read = await _transport.TransportStream.ReadAsync(
                    buffer.AsMemory(bytesInBuffer, buffer.Length - bytesInBuffer),
                    cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await HandleUnexpectedDisconnectAsync(
                            new EndOfStreamException("The RDP server closed the transport stream.")).ConfigureAwait(false);
                    }
                    break;
                }

                bytesInBuffer += read;

                while (bytesInBuffer > 0)
                {
                    byte b1 = buffer[0];
                    bool processedPacket = false;

                    // Check if FastPath Action (bits 0..1 == 0x00)
                    if ((b1 & 0x03) == 0x00)
                    {
                        // FastPath server header is 2 bytes if (buffer[1] & 0x80) == 0, or 3 bytes if (buffer[1] & 0x80) != 0.
                        // If we don't have enough header bytes in buffer, break and wait for network stream.
                        if (bytesInBuffer < 2 || ((buffer[1] & 0x80) != 0 && bytesInBuffer < 3))
                        {
                            break;
                        }

                        RdpPacketReader reader = new RdpPacketReader(buffer.AsSpan(0, bytesInBuffer));
                        if (RdpFastPathFrameReader.TryReadFastPathHeader(ref reader, out FastPathServerHeader header))
                        {
                            if (header.PacketLength > MaxFastPathPacketLength || header.PacketLength < header.HeaderLength || header.PacketLength >= buffer.Length)
                            {
                                // Invalid FastPath packet length larger than buffer or limits; discard 1 noise byte
                                DiscardBytes(buffer, ref bytesInBuffer, 1);
                                continue;
                            }

                            if (bytesInBuffer < header.PacketLength)
                            {
                                // Full FastPath PDU not yet arrived
                                break;
                            }

                            ReadOnlyMemory<byte> pduMemory = buffer.AsMemory(0, header.PacketLength);

                            if (_fastPathAssembler.TryProcessPacket(
                                pduMemory,
                                Interlocked.Increment(ref _frameIdCounter),
                                DateTimeOffset.UtcNow,
                                out RdpFrameUpdateEventArgs? frameArgs) && frameArgs != null)
                            {
                                FrameUpdated?.Invoke(this, frameArgs);
                            }

                            DiscardBytes(buffer, ref bytesInBuffer, header.PacketLength);
                            processedPacket = true;
                        }
                    }

                    if (processedPacket) continue;

                    // Check if TPKT Header (starts with 0x03)
                    if (b1 == TpktHeader.ExpectedVersion)
                    {
                        if (bytesInBuffer < TpktHeader.HeaderLength)
                        {
                            break; // Wait for full 4-byte TPKT header
                        }

                        RdpPacketReader tpktReader = new RdpPacketReader(buffer.AsSpan(0, bytesInBuffer));
                        if (TpktHeader.TryRead(ref tpktReader, out TpktHeader tpkt))
                        {
                            if (tpkt.PacketLength > MaxTpktPacketLength || tpkt.PacketLength < TpktHeader.HeaderLength || tpkt.PacketLength >= buffer.Length)
                            {
                                // Invalid TPKT packet length larger than buffer or limits; discard 1 noise byte
                                DiscardBytes(buffer, ref bytesInBuffer, 1);
                                continue;
                            }

                            if (bytesInBuffer < tpkt.PacketLength)
                            {
                                break; // Wait for full TPKT packet
                            }

                            DiscardBytes(buffer, ref bytesInBuffer, tpkt.PacketLength);
                            processedPacket = true;
                        }
                    }

                    if (processedPacket) continue;

                    // Unrecognized or corrupt prefix byte: discard 1 byte to advance stream
                    DiscardBytes(buffer, ref bytesInBuffer, 1);
                }

                if (bytesInBuffer == buffer.Length)
                {
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, 0, newBuffer, 0, bytesInBuffer);
                    buffer = newBuffer;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal loop cancellation
        }
        catch (Exception ex)
        {
            await HandleUnexpectedDisconnectAsync(ex).ConfigureAwait(false);
        }
    }

    private async Task HandleUnexpectedDisconnectAsync(Exception exception)
    {
        if (_state is RdpConnectionState.Disconnecting or RdpConnectionState.Disconnected)
        {
            return;
        }

        SetState(RdpConnectionState.Faulted, exception);

        IRdpSecurityTransport? transport = Interlocked.Exchange(ref _transport, null);
        if (transport != null)
        {
            try { await transport.DisposeAsync().ConfigureAwait(false); } catch { }
        }

        try { _networkStream?.Dispose(); } catch { }
        _networkStream = null;
        try { _tcpClient?.Dispose(); } catch { }
        _tcpClient = null;
    }

    private static void DiscardBytes(byte[] buffer, ref int bytesInBuffer, int count)
    {
        int remaining = bytesInBuffer - count;
        if (remaining > 0)
        {
            Array.Copy(buffer, count, buffer, 0, remaining);
        }
        bytesInBuffer = remaining;
    }

    public async Task SendInputEventAsync(RdpInputEvent inputEvent, CancellationToken cancellationToken = default)
    {
        if (_state != RdpConnectionState.Connected || _transport == null || Volatile.Read(ref _isDisposed) != 0)
            throw new InvalidOperationException("Client is not connected.");

        try
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Client is not connected.");
        }
        try
        {
            if (_state != RdpConnectionState.Connected || _transport == null || Volatile.Read(ref _isDisposed) != 0)
                throw new InvalidOperationException("Client is not connected.");

            byte[] packet;
            int packetLength;
            if (_options.EnableFastPath)
            {
                byte[] eventBuffer = new byte[16];
                var eventWriter = new RdpPacketWriter(eventBuffer);
                WriteFastPathEquivalent(ref eventWriter, inputEvent);

                packet = new byte[eventWriter.WrittenCount + 3];
                var packetWriter = new RdpPacketWriter(packet);
                ushort fastPathLength = checked((ushort)(eventWriter.WrittenCount + 2));
                RdpInputPduWriter.WriteFastPathHeader(ref packetWriter, numEvents: 1, pduLength: fastPathLength);
                packetWriter.WriteSpan(eventBuffer.AsSpan(0, eventWriter.WrittenCount));
                packetLength = packetWriter.WrittenCount;
            }
            else
            {
                packet = RdpActivationSequence.CreateSlowPathInputPacket(
                    _userId,
                    _ioChannelId,
                    _shareId,
                    inputEvent);
                packetLength = packet.Length;
            }

            await _transport.TransportStream.WriteAsync(packet.AsMemory(0, packetLength), cancellationToken).ConfigureAwait(false);
            await _transport.TransportStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _sendLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public async Task SendFastPathInputEventAsync(RdpFastPathInputEvent inputEvent, CancellationToken cancellationToken = default)
    {
        if (_state != RdpConnectionState.Connected || _transport == null || Volatile.Read(ref _isDisposed) != 0)
            throw new InvalidOperationException("Client is not connected.");

        try
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            throw new InvalidOperationException("Client is not connected.");
        }
        try
        {
            if (_state != RdpConnectionState.Connected || _transport == null || Volatile.Read(ref _isDisposed) != 0)
                throw new InvalidOperationException("Client is not connected.");

            byte[] eventBuffer = new byte[16];
            var eventWriter = new RdpPacketWriter(eventBuffer);
            inputEvent.Write(ref eventWriter);

            byte[] packet = new byte[eventWriter.WrittenCount + 3];
            var packetWriter = new RdpPacketWriter(packet);
            ushort packetLength = checked((ushort)(eventWriter.WrittenCount + 2));
            RdpInputPduWriter.WriteFastPathHeader(ref packetWriter, numEvents: 1, pduLength: packetLength);
            packetWriter.WriteSpan(eventBuffer.AsSpan(0, eventWriter.WrittenCount));
            await _transport.TransportStream.WriteAsync(packet.AsMemory(0, packetWriter.WrittenCount), cancellationToken).ConfigureAwait(false);
            await _transport.TransportStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _sendLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private static void WriteFastPathEquivalent(ref RdpPacketWriter writer, in RdpInputEvent inputEvent)
    {
        switch (inputEvent.MessageType)
        {
            case RdpInputMessageType.ScanCode:
            {
                FastPathKeyboardFlags flags = FastPathKeyboardFlags.None;
                if ((inputEvent.KeyboardEvent.Flags & RdpKeyboardFlags.Release) != 0)
                    flags |= FastPathKeyboardFlags.Release;
                if ((inputEvent.KeyboardEvent.Flags & RdpKeyboardFlags.Extended) != 0)
                    flags |= FastPathKeyboardFlags.Extended;
                if ((inputEvent.KeyboardEvent.Flags & RdpKeyboardFlags.Extended1) != 0)
                    flags |= FastPathKeyboardFlags.Extended1;
                new RdpFastPathInputEvent(flags, checked((byte)inputEvent.KeyboardEvent.KeyCode)).Write(ref writer);
                break;
            }

            case RdpInputMessageType.Unicode:
            {
                FastPathKeyboardFlags flags = (inputEvent.KeyboardEvent.Flags & RdpKeyboardFlags.Release) != 0
                    ? FastPathKeyboardFlags.Release
                    : FastPathKeyboardFlags.None;
                new RdpFastPathInputEvent(
                    flags,
                    checked((ushort)inputEvent.KeyboardEvent.KeyCode)).Write(ref writer);
                break;
            }

            case RdpInputMessageType.Mouse:
            case RdpInputMessageType.MouseX:
                new RdpFastPathInputEvent(
                    inputEvent.MessageType == RdpInputMessageType.Mouse
                        ? FastPathInputEventCode.Mouse
                        : FastPathInputEventCode.MouseX,
                    inputEvent.MouseEvent.PointerFlags,
                    inputEvent.MouseEvent.XPos,
                    inputEvent.MouseEvent.YPos).Write(ref writer);
                break;

            case RdpInputMessageType.Sync:
                new RdpFastPathInputEvent((byte)inputEvent.SyncEvent.ToggleFlags).Write(ref writer);
                break;

            default:
                throw new NotSupportedException($"Fast-Path input does not support {inputEvent.MessageType}.");
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return;

        try
        {
            await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (_state == RdpConnectionState.Disconnected || _state == RdpConnectionState.Disconnecting)
                return;

            SetState(RdpConnectionState.Disconnecting);
            await CleanupAsync().ConfigureAwait(false);
            SetState(RdpConnectionState.Disconnected);
        }
        finally
        {
            try
            {
                _stateLock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private async Task CleanupAsync()
    {
        if (_cts != null)
        {
            try { _cts.Cancel(); } catch { }
        }

        if (_transport != null)
        {
            try { await _transport.DisposeAsync().ConfigureAwait(false); } catch { }
            _transport = null;
        }

        try { _networkStream?.Dispose(); } catch { }
        _networkStream = null;

        try { _tcpClient?.Dispose(); } catch { }
        _tcpClient = null;

        if (_receiveLoopTask != null)
        {
            try { await _receiveLoopTask.ConfigureAwait(false); } catch { }
            _receiveLoopTask = null;
        }

        if (_cts != null)
        {
            try { _cts.Dispose(); } catch { }
            _cts = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _stateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_state != RdpConnectionState.Disconnected && _state != RdpConnectionState.Disconnecting)
                {
                    SetState(RdpConnectionState.Disconnecting);
                    await CleanupAsync().ConfigureAwait(false);
                    SetState(RdpConnectionState.Disconnected);
                }
            }
            finally
            {
                try
                {
                    _stateLock.Release();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }

        _stateLock.Dispose();
        _sendLock.Dispose();
    }

    internal void RaiseFrameUpdatedForTesting(RdpFrameUpdateEventArgs args)
    {
        FrameUpdated?.Invoke(this, args);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
