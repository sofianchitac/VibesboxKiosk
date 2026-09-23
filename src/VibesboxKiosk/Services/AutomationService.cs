using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VibesboxKiosk.Config;

namespace VibesboxKiosk.Services;

/// <summary>
/// Runs the configured automations: watch a boolean field in a WebSocket JSON
/// feed and, on its rising edge, send an OSC value — consulting the OSC feedback
/// first so a manual override is never fought (see <see cref="AutomationConfig"/>).
///
/// Example: a source device reports <c>"multichannel": true</c> → turn the
/// upmixer off; <c>"stereo": true</c> → turn it back on. Each direction fires
/// only on its own rising edge, so a user override mid-programme sticks until
/// the format changes again.
///
/// Last-seen flag and feedback values outlive a restart (config reload), so a
/// reload doesn't re-fire an edge against content that never changed.
/// </summary>
public static class AutomationService
{
    private static readonly Dictionary<string, bool>  LastFlags    = new();
    private static readonly Dictionary<string, float> LastFeedback = new();
    private static readonly List<Connection> Connections = new();
    private static readonly List<(string Address, Action<float> Handler)> Subs = new();

    public static void Start(IReadOnlyList<AutomationConfig> rules)
    {
        Stop();

        var active = rules.Where(r => r.Enabled && r.Url.Length > 0 && r.Field.Length > 0 && r.Address.Length > 0).ToList();

        foreach (var address in active.Select(FeedbackOf).Distinct())
        {
            Action<float> handler = v => { lock (LastFeedback) LastFeedback[address] = v; };
            OscService.Instance.Subscribe(address, handler);
            Subs.Add((address, handler));
        }

        foreach (var group in active.GroupBy(r => r.Url))
        {
            var c = new Connection(group.Key, group.ToList());
            Connections.Add(c);
            c.Start();
        }
    }

    public static void Stop()
    {
        foreach (var c in Connections) c.Stop();
        Connections.Clear();
        foreach (var (address, handler) in Subs)
            OscService.Instance.Unsubscribe(address, handler);
        Subs.Clear();
    }

    private static string FeedbackOf(AutomationConfig r) =>
        string.IsNullOrEmpty(r.FeedbackAddress) ? r.Address : r.FeedbackAddress;

    private static void OnRisingEdge(AutomationConfig r)
    {
        float? fb;
        lock (LastFeedback) fb = LastFeedback.TryGetValue(FeedbackOf(r), out var v) ? v : null;

        if (fb is float known && Math.Abs(known - r.Value) < 0.5f)
        {
            Log.Info($"[Automation] {r.Name}: {r.Field} rose; already at {known} — no action.");
            return;
        }
        if (fb is null && !r.SendWhenUnknown)
        {
            Log.Info($"[Automation] {r.Name}: {r.Field} rose; state unknown — no action.");
            return;
        }

        Log.Info($"[Automation] {r.Name}: {r.Field} rose → {r.Address} = {r.Value}");
        OscService.Instance.Send(r.Address, r.Value);
    }

    private sealed class Connection : WsSubscriber
    {
        private readonly string _url;
        private readonly List<AutomationConfig> _rules;

        public Connection(string url, List<AutomationConfig> rules) { _url = url; _rules = rules; }

        protected override string Name => "Automation";

        public void Start() => StartLoop(_url);
        public void Stop()  => StopLoop();

        protected override void HandleFrame(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;

            foreach (var field in _rules.Select(r => r.Field).Distinct())
            {
                if (!root.TryGetProperty(field, out var v) ||
                    (v.ValueKind != JsonValueKind.True && v.ValueKind != JsonValueKind.False))
                    continue;

                bool now = v.GetBoolean();
                string key = _url + "|" + field;
                bool was;
                lock (LastFlags)
                {
                    was = LastFlags.TryGetValue(key, out var w) && w;
                    LastFlags[key] = now;
                }
                if (now && !was)
                    foreach (var r in _rules.Where(r => r.Field == field))
                        OnRisingEdge(r);
            }
        }
    }
}
