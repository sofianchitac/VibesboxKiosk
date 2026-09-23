using System;
using System.Buffers;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using VibesboxKiosk.Config;
using VibesboxKiosk.Controls;

namespace VibesboxKiosk.Services;

/// <summary>
/// Subscribes to the configured per-band OSC addresses, exponential-smooths
/// each band, and pushes a float[] to SpectrumView at ≥30 Hz.
///
/// Lifecycle: call Start() after OscService is running, Stop() on teardown.
/// </summary>
public sealed class SpectrumService
{
    public static SpectrumService Instance { get; } = new();

    private const float AttackAlpha = 0.4f;   // fast attack — new peaks appear quickly
    private const float ReleaseAlpha = 0.15f;  // moderate release — decay is smooth

    private int _bandCount;
    private float _floorDb;
    private readonly List<(string Address, Action<float> Handler)> _subs = new();
    private float[] _rawBands = Array.Empty<float>();
    private float[] _smoothedBands = Array.Empty<float>();

    private SpectrumView? _view;
    private DispatcherQueue? _dispatcher;
    private DispatcherQueueTimer? _timer;

    private SpectrumService() { }

    /// <summary>
    /// Starts subscribing to spectrum OSC messages and pushing updates to the view.
    /// </summary>
    public void Start(SpectrumConfig config, SpectrumView view, DispatcherQueue dispatcher)
    {
        Stop();
        if (!config.Enabled) return;

        _view = view;
        _dispatcher = dispatcher;
        _bandCount = config.BandCount;
        _floorDb = (float)config.FloorDb;

        // Start both buffers at the silence floor
        _rawBands = new float[_bandCount];
        _smoothedBands = new float[_bandCount];
        Array.Fill(_rawBands, _floorDb);
        Array.Fill(_smoothedBands, _floorDb);

        for (int i = 0; i < _bandCount; i++)
        {
            int bandIndex = i; // capture for closure
            string address = string.Format(config.AddressPattern, bandIndex);
            Action<float> handler = value => OnBandReceived(bandIndex, value);
            OscService.Instance.Subscribe(address, handler);
            _subs.Add((address, handler));
        }

        // 30 Hz update timer on the UI thread
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += OnTimerTick;
        _timer.Start();

        Log.Info($"[Spectrum] Started — {_bandCount} bands, 30 Hz update.");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;

        foreach (var (address, handler) in _subs)
            OscService.Instance.Unsubscribe(address, handler);
        _subs.Clear();

        _view = null;
        _dispatcher = null;

        Log.Info("[Spectrum] Stopped.");
    }

    // ── Callbacks ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the OscService receive thread when a band value arrives.
    /// Thread-safe: only writes to a single array slot (atomic for float on x64).
    /// </summary>
    private void OnBandReceived(int index, float value)
    {
        if (index >= 0 && index < _rawBands.Length)
            _rawBands[index] = value;
    }

    /// <summary>
    /// Runs on the UI thread at ~30 Hz. Applies exponential smoothing and
    /// pushes the result to SpectrumView.
    /// </summary>
    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (_view is null) return;

        // Rent a temporary buffer from ArrayPool to avoid per-frame allocation
        var output = ArrayPool<float>.Shared.Rent(_bandCount);

        try
        {
            for (int i = 0; i < _bandCount; i++)
            {
                float raw = _rawBands[i];
                float prev = _smoothedBands[i];

                // Asymmetric smoothing: fast attack (new energy), slow release (decay)
                float alpha = raw > prev ? AttackAlpha : ReleaseAlpha;
                float smoothed = alpha * raw + (1f - alpha) * prev;

                _smoothedBands[i] = smoothed;
                output[i] = smoothed;
            }

            // UpdateBands expects exactly _bandCount elements; the rented array
            // may be larger, so we pass a properly-sized copy via Span.
            var sized = output.AsSpan(0, _bandCount).ToArray();
            _view.UpdateBands(sized);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(output);
        }
    }
}
