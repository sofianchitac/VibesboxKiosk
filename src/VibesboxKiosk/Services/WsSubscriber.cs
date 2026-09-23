using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VibesboxKiosk.Services;

/// <summary>
/// Shared resilient-WebSocket subscriber: connect, reassemble fragmented text
/// frames, hand each complete frame to the subclass, reconnect with 2s→30s
/// exponential backoff, and drain the receive loop off the calling thread on
/// Stop. Extracted from NowPlayingService (AutoUpmixService had cloned it) so
/// reconnect/keep-alive behavior can't drift between subscribers.
///
/// Zero new NuGet packages — System.Net.WebSockets built into .NET.
/// </summary>
public abstract class WsSubscriber
{
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    /// <summary>Log prefix, e.g. "NowPlaying".</summary>
    protected abstract string Name { get; }

    /// <summary>One complete (reassembled) text frame. Exceptions are caught
    /// and logged as a malformed payload; the connection stays up.</summary>
    protected abstract void HandleFrame(string json);

    protected virtual void OnConnecting()    { }
    protected virtual void OnConnected()     { }
    protected virtual void OnDisconnected()  { }

    /// <summary>(Re)start the receive loop against <paramref name="wsUrl"/>.</summary>
    protected void StartLoop(string wsUrl)
    {
        StopLoop();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _runTask = Task.Run(() => RunAsync(wsUrl, ct));
    }

    /// <summary>
    /// Stop and drain the receive loop OFF the calling thread — Stop runs on
    /// the UI thread during a config hot-reload; a synchronous Wait() here
    /// would freeze the UI for up to the drain timeout on every reload.
    /// </summary>
    protected void StopLoop()
    {
        var cts  = _cts;
        var task = _runTask;
        _cts     = null;
        _runTask = null;
        if (cts is null) return;

        cts.Cancel();
        _ = Task.Run(() =>
        {
            try { task?.Wait(2000); } catch { /* swallow shutdown/cancel errors */ }
            finally { cts.Dispose(); }
        });
    }

    // ── connect / reconnect loop ─────────────────────────────────────────

    private async Task RunAsync(string wsUrl, CancellationToken ct)
    {
        TimeSpan backoff = TimeSpan.FromSeconds(2);
        while (!ct.IsCancellationRequested)
        {
            OnConnecting();
            try
            {
                await ConnectAndConsume(wsUrl, ct);
                backoff = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                Log.Warn($"{Name} WS disconnected: {ex.Message}");
            }
            OnDisconnected();

            try { await Task.Delay(backoff, ct); }
            catch (OperationCanceledException) { return; }
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    private async Task ConnectAndConsume(string wsUrl, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
        Log.Info($"{Name}: connecting to {wsUrl}");
        await ws.ConnectAsync(new Uri(wsUrl), ct);
        OnConnected();
        Log.Info($"{Name}: connected");

        var buffer   = new byte[64 * 1024];
        using var ms = new MemoryStream();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult res;
            do
            {
                res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (res.MessageType == WebSocketMessageType.Close)
                    return;
                ms.Write(buffer, 0, res.Count);
            } while (!res.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            try { HandleFrame(json); }
            catch (Exception ex)
            {
                Log.Warn($"{Name}: malformed payload — {ex.Message}");
            }
        }
    }
}
