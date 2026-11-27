using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using VibesboxKiosk.Config;
using VibesboxKiosk.Controls;

namespace VibesboxKiosk.Services;

/// <summary>
/// Drives the footer indicators at 30 Hz:
/// • input meters — one dot per channel, brightness = level (0..1) from OSC;
/// • activity dots — flash on any message to their address, or on the kiosk's
///   own outbound OSC for <see cref="ActivityConfig.OscOut"/>.
/// The input meters also wake the display when audio starts after a quiet spell
/// (<see cref="PowerConfig.WakeOnAudio"/>).
/// </summary>
public sealed class IndicatorService
{
    public static IndicatorService Instance { get; } = new();

    // A channel / activity dot stays lit this long after its last signal.
    private const long HoldMs = 200;

    private float _threshold;
    private float[] _rawLevels = Array.Empty<float>();
    private float[] _intensities = Array.Empty<float>();
    private long[] _lastActiveTicks = Array.Empty<long>();
    private long _wakeQuietMs;      // 0 = wake on audio is off
    private long _lastAudioTicks;   // last time any channel was at/above threshold

    private readonly List<(IndicatorDotRow Row, string Address)> _activity = new();
    private readonly Dictionary<string, long> _lastActivityTicks = new();
    private readonly List<(string Address, Action<float> Handler)> _subs = new();

    private InputDotBar? _inputDotBar;
    private DispatcherQueueTimer? _timer;

    private IndicatorService() { }

    public void Start(
        KioskConfig config,
        InputDotBar inputDotBar,
        IReadOnlyList<IndicatorDotRow> activityRows,
        DispatcherQueue dispatcher)
    {
        Stop();

        var meters = config.InputMeters;
        int count = meters.Enabled ? meters.Count : 0;
        _threshold       = (float)meters.Threshold;
        _rawLevels       = new float[count];
        _intensities     = new float[count];
        _lastActiveTicks = new long[count];
        _inputDotBar     = inputDotBar;
        _wakeQuietMs     = config.Power.WakeOnAudio ? (long)(config.Power.WakeQuietSeconds * 1000) : 0;
        // A (re)start mid-programme is not audio "starting".
        _lastAudioTicks  = Environment.TickCount64;

        for (int i = 0; i < count; i++)
        {
            int index = i;
            Subscribe(string.Format(meters.AddressPattern, i + 1), v => OnChannelMeter(index, v));
        }

        for (int i = 0; i < activityRows.Count && i < config.Activity.Count; i++)
        {
            string address = config.Activity[i].Address;
            _activity.Add((activityRows[i], address));
            if (string.IsNullOrEmpty(address) || address == ActivityConfig.OscOut) continue;

            lock (_lastActivityTicks) _lastActivityTicks[address] = 0;
            Subscribe(address, _ => { lock (_lastActivityTicks) _lastActivityTicks[address] = Environment.TickCount64; });
        }

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTimerTick;
        _timer.Start();

        Log.Info($"[Indicators] Started — {count} input meters, {_activity.Count} activity dots");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        foreach (var (address, handler) in _subs)
            OscService.Instance.Unsubscribe(address, handler);
        _subs.Clear();
        _activity.Clear();
        lock (_lastActivityTicks) _lastActivityTicks.Clear();
        _inputDotBar = null;
    }

    private void Subscribe(string address, Action<float> handler)
    {
        OscService.Instance.Subscribe(address, handler);
        _subs.Add((address, handler));
    }

    // ── OSC callbacks (receive thread) ───────────────────────────────────

    private void OnChannelMeter(int index, float value)
    {
        var levels = _rawLevels;
        if (index >= levels.Length) return;
        levels[index] = value;
        if (value < _threshold) return;

        long now = Environment.TickCount64;
        _lastActiveTicks[index] = now;
        if (_wakeQuietMs <= 0) return;

        long last = Interlocked.Exchange(ref _lastAudioTicks, now);
        if (now - last >= _wakeQuietMs)
        {
            Log.Info($"[Display] Audio after {(now - last) / 1000} s quiet — waking display.");
            DisplaySleepService.WakeDisplay();
        }
    }

    // ── UI thread (30 Hz) ────────────────────────────────────────────────

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        long now = Environment.TickCount64;

        if (_inputDotBar is not null)
        {
            for (int i = 0; i < _intensities.Length; i++)
            {
                float raw = _rawLevels[i];
                bool active = raw >= _threshold || (now - _lastActiveTicks[i]) < HoldMs;
                _intensities[i] = active ? Math.Clamp(raw, 0f, 1f) : 0f;
            }
            _inputDotBar.SetIntensities(_intensities);
        }

        foreach (var (row, address) in _activity)
        {
            bool lit;
            if (address == ActivityConfig.OscOut)
                lit = OscService.Instance.IsSending;
            else
                lock (_lastActivityTicks)
                    lit = _lastActivityTicks.TryGetValue(address, out var t) && now - t < HoldMs;

            // ActiveMask is a DependencyProperty; identical writes are coalesced.
            row.ActiveMask = lit ? 1 : 0;
        }
    }
}
