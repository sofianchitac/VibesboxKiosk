using System;
using System.Collections.Generic;
using Microsoft.UI.Dispatching;
using VibesboxKiosk.Config;

namespace VibesboxKiosk.Services;

public class HudRequestedEventArgs : EventArgs
{
    public string Label { get; }
    public string Value { get; }

    public HudRequestedEventArgs(string label, string value)
    {
        Label = label;
        Value = value;
    }
}

public class OverlayService
{
    private static readonly Lazy<OverlayService> _instance = new(() => new OverlayService());
    public static OverlayService Instance => _instance.Value;

    public event EventHandler<HudRequestedEventArgs>? HudRequested;
    public event EventHandler? PowerRequested;

    private readonly Dictionary<string, Action<float>> _oscHandlers = new();

    private OverlayService() { }

    /// <summary>
    /// Subscribes to OSC feedback addresses for all overlays.
    /// Marshals HUD requests to the UI thread via the provided dispatcher.
    /// </summary>
    public void Start(IReadOnlyList<OverlayConfig> configs, DispatcherQueue dispatcher)
    {
        Stop();

        foreach (var config in configs)
        {
            if (string.IsNullOrEmpty(config.OscFeedback)) continue;

            Action<float> handler = value =>
            {
                dispatcher.TryEnqueue(() =>
                {
                    ShowHud(config.Label, ValueFormat.Apply(config.Format, value));
                });
            };

            _oscHandlers[config.OscFeedback] = handler;
            OscService.Instance.Subscribe(config.OscFeedback, handler);
        }
    }

    public void Stop()
    {
        foreach (var kvp in _oscHandlers)
        {
            OscService.Instance.Unsubscribe(kvp.Key, kvp.Value);
        }
        _oscHandlers.Clear();
    }

    public void ShowHud(string label, string value)
    {
        HudRequested?.Invoke(this, new HudRequestedEventArgs(label, value));
    }

    public void ShowPower()
    {
        PowerRequested?.Invoke(this, EventArgs.Empty);
    }
}
