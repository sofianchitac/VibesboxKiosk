using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VibesboxKiosk.Config;

namespace VibesboxKiosk.Services;

/// <summary>
/// Bidirectional OSC over UDP. No third-party library — manually encodes/decodes
/// the OSC wire format (address + type tag + float, all big-endian, 4-byte aligned).
/// 
/// Send:    OscService.Instance.Send("/kiosk/01", 1.0f);
/// Receive: OscService.Instance.Subscribe("/kiosk/01", v => { ... });
/// </summary>
public sealed class OscService
{
    public static OscService Instance { get; } = new();

    private UdpClient? _sender;
    private UdpClient? _receiver;
    private CancellationTokenSource? _cts;
    private IPEndPoint? _target;

    // ── Subscriber registry ──────────────────────────────────────────────
    // OSC address → callback. Multiple subscribers per address are combined
    // via multicast delegate.
    private readonly ConcurrentDictionary<string, Action<float>> _subscribers = new();

    // ── Outbound activity tracking (drives OSC indicator dot) ────────────
    private long _lastSendTicks;

    /// <summary>
    /// True when the kiosk has sent an OSC packet within the last ~1 second.
    /// Polled by IndicatorService at 30 Hz.
    /// </summary>
    public bool IsSending => _sender != null
        && Environment.TickCount64 - _lastSendTicks < 1000;

    private OscService() { }

    // ── Lifecycle ────────────────────────────────────────────────────────

    public void Start(OscConfig config)
    {
        Stop();

        try
        {
            _target = new IPEndPoint(ResolveHost(config.Host), config.SendPort);
            _sender = new UdpClient();

            // A local host only needs loopback; binding there (instead of all
            // interfaces) also avoids the Windows Firewall prompt on first run.
            var bind = IPAddress.IsLoopback(_target.Address) ? IPAddress.Loopback : IPAddress.Any;
            _receiver = new UdpClient(new IPEndPoint(bind, config.ListenPort));
            _cts = new CancellationTokenSource();

            // Fire-and-forget receive loop
            _ = ReceiveLoopAsync(_cts.Token);

            Log.Info(
                $"[OSC] Started — send to {config.Host}:{config.SendPort}, " +
                $"listen on :{config.ListenPort}");
        }
        catch (Exception ex)
        {
            Log.Error($"[OSC] Failed to start: {ex.Message}");
            Stop();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();

        _sender?.Dispose();
        _sender = null;
        _receiver?.Dispose();
        _receiver = null;
        _cts?.Dispose();
        _cts = null;

        _subscribers.Clear();

        Log.Info("[OSC] Stopped.");
    }

    // ── Send ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an OSC message with a single float argument to the DSP host.
    /// </summary>
    public void Send(string address, float value)
    {
        if (_sender is null || _target is null) return;

        try
        {
            var packet = EncodeMessage(address, value);
            _sender.Send(packet, packet.Length, _target);

            _lastSendTicks = Environment.TickCount64;
        }
        catch (Exception ex)
        {
            Log.Warn($"[OSC] Send error: {ex.Message}");
        }
    }

    // ── Subscribe / Unsubscribe ──────────────────────────────────────────

    /// <summary>
    /// Registers a callback for a specific OSC address. The callback fires
    /// on the receive-loop background thread — callers must marshal to the
    /// UI thread if they need to update controls.
    /// </summary>
    public void Subscribe(string address, Action<float> callback)
    {
        _subscribers.AddOrUpdate(
            address,
            callback,
            (_, existing) => existing + callback);
    }

    /// <summary>
    /// Removes a previously registered callback for an OSC address.
    /// </summary>
    public void Unsubscribe(string address, Action<float> callback)
    {
        if (!_subscribers.TryGetValue(address, out var current))
            return;

        var updated = (Action<float>?)Delegate.Remove(current, callback);
        if (updated is null)
            _subscribers.TryRemove(address, out _);
        else
            _subscribers[address] = updated;
    }

    // ── Receive loop ─────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _receiver!.ReceiveAsync(ct);
                ProcessPacket(result.Buffer);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Warn($"[OSC] Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        if (data.Length < 4) return;

        // OSC bundles start with '#' ("#bundle\0")
        if (data[0] == (byte)'#')
        {
            ProcessBundle(data);
            return;
        }

        // Single message
        ProcessMessage(data, 0, data.Length);
    }

    private void ProcessBundle(byte[] data)
    {
        // Bundle header: "#bundle\0" (8 bytes) + timetag (8 bytes) = 16 bytes minimum
        if (data.Length < 16) return;

        int offset = 16;
        while (offset < data.Length - 4)
        {
            int size = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset));
            offset += 4;

            if (size <= 0 || offset + size > data.Length) break;

            ProcessMessage(data, offset, size);
            offset += size;
        }
    }

    private void ProcessMessage(byte[] data, int offset, int length)
    {
        int end = offset + length;

        // ── Read address (null-terminated, 4-byte aligned) ───────────────
        int addrEnd = Array.IndexOf(data, (byte)0, offset, length);
        if (addrEnd < 0) return;

        string address = Encoding.ASCII.GetString(data, offset, addrEnd - offset);
        int pos = Align4(addrEnd + 1);

        // ── Read type tag string (",f\0\0" etc.) ─────────────────────────
        if (pos >= end || data[pos] != (byte)',') return;

        int tagEnd = Array.IndexOf(data, (byte)0, pos, end - pos);
        if (tagEnd < 0) return;

        string typeTags = Encoding.ASCII.GetString(data, pos + 1, tagEnd - pos - 1);
        pos = Align4(tagEnd + 1);

        // ── Extract first argument as float ──────────────────────────────
        float value = 0f;
        if (typeTags.Length > 0 && pos + 4 <= end)
        {
            switch (typeTags[0])
            {
                case 'f':
                    value = BinaryPrimitives.ReadSingleBigEndian(data.AsSpan(pos));
                    break;
                case 'i':
                    value = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
                    break;
                case 'd' when pos + 8 <= end:
                    value = (float)BinaryPrimitives.ReadDoubleBigEndian(data.AsSpan(pos));
                    break;
            }
        }

        // ── Dispatch ─────────────────────────────────────────────────────
        if (_subscribers.TryGetValue(address, out var callback))
        {
            try
            {
                callback(value);
            }
            catch (Exception ex)
            {
                Log.Warn($"[OSC] Subscriber error on '{address}': {ex.Message}");
            }
        }
    }

    // ── OSC wire-format encoding ─────────────────────────────────────────

    /// <summary>
    /// Encodes a single OSC message: address + ",f" type tag + one float value.
    /// All strings are null-terminated and padded to 4-byte boundaries.
    /// Float is written big-endian per the OSC 1.0 spec.
    /// </summary>
    private static byte[] EncodeMessage(string address, float value)
    {
        byte[] addrBytes = Encoding.ASCII.GetBytes(address);
        int addrPadded = Align4(addrBytes.Length + 1); // +1 for null terminator

        // Type tag: ",f\0" padded to 4 bytes
        const int typeTagSize = 4;
        const int valueSize = 4;

        byte[] packet = new byte[addrPadded + typeTagSize + valueSize];

        // Address (remaining bytes are zero = null padding)
        Buffer.BlockCopy(addrBytes, 0, packet, 0, addrBytes.Length);

        // Type tag
        int typeOffset = addrPadded;
        packet[typeOffset] = (byte)',';
        packet[typeOffset + 1] = (byte)'f';

        // Float value (big-endian)
        BinaryPrimitives.WriteSingleBigEndian(
            packet.AsSpan(typeOffset + typeTagSize), value);

        return packet;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IPAddress ResolveHost(string host)
    {
        if (IPAddress.TryParse(host, out var ip)) return ip;
        foreach (var a in Dns.GetHostAddresses(host))
            if (a.AddressFamily == AddressFamily.InterNetwork) return a;
        throw new ArgumentException($"cannot resolve '{host}'");
    }

    /// <summary>Round up to the next 4-byte boundary.</summary>
    private static int Align4(int pos) => (pos + 3) & ~3;
}
