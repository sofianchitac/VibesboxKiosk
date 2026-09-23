using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VibesboxKiosk.Services;

/// <summary>
/// Fire-and-forget WebSocket message (used by the logo toggle).
///
/// Deliberately NOT a persistent connection: connect per message with a short
/// timeout and swallow failures — an unreachable peer must never affect the
/// kiosk, and an occasional toggle isn't worth a keep-alive socket.
/// </summary>
public static class WsCommand
{
    public static void Send(string url, string message)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrEmpty(message)) return;
        _ = Task.Run(() => SendAsync(url, message));
    }

    private static async Task SendAsync(string url, string message)
    {
        try
        {
            using var ws  = new ClientWebSocket();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await ws.ConnectAsync(new Uri(url), cts.Token);
            await ws.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cts.Token);
            Log.Info($"[WsCommand] sent to {url}");
        }
        catch (Exception ex)
        {
            Log.Info($"[WsCommand] {url} unreachable ({ex.Message}) — skipped");
        }
    }
}
