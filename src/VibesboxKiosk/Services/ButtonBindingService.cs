using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using VibesboxKiosk.Config;
using VibesboxKiosk.Controls;

namespace VibesboxKiosk.Services;

public sealed class ButtonBindingService
{
    public static ButtonBindingService Instance { get; } = new();

    private readonly Dictionary<string, ButtonBinding> _bindings = new();

    private ButtonBindingService() { }

    /// <summary>Replaces every existing binding (the dashboard rebuilds its buttons on each config change).</summary>
    public void Bind(
        IReadOnlyDictionary<string, ActionButton> controls,
        IReadOnlyList<ActionButtonConfig>          configs)
    {
        Clear();

        foreach (var config in configs)
        {
            if (!controls.TryGetValue(config.Id, out var button))
                continue;

            var binding = new ButtonBinding(button, config, stateIndex: 0);
            _bindings[config.Id] = binding;
            Attach(binding);
            ApplyState(binding);
        }
    }

    public void Clear()
    {
        foreach (var b in _bindings.Values)
            Detach(b);
        _bindings.Clear();
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    private void Attach(ButtonBinding b)
    {
        b.Button.ButtonTapped   += b.OnTapped;
        b.Button.ButtonPressed  += b.OnPressed;
        b.Button.ButtonReleased += b.OnReleased;
    }

    private void Detach(ButtonBinding b)
    {
        b.Button.ButtonTapped   -= b.OnTapped;
        b.Button.ButtonPressed  -= b.OnPressed;
        b.Button.ButtonReleased -= b.OnReleased;
        b.UnsubscribeOsc();
    }

    // ── State helpers ────────────────────────────────────────────────────────

    private static void ApplyState(ButtonBinding b)
    {
        var s = b.Config.States[b.StateIndex];
        b.Button.Label  = b.Config.Name;
        b.Button.Status = s.Label;
        b.Button.State  = ParseState(s.Name);
    }

    private static ActionButtonState ParseState(string name) => name.ToLowerInvariant() switch
    {
        "playing" => ActionButtonState.Playing,
        "active"  => ActionButtonState.Active,
        "off"     => ActionButtonState.Off,
        _         => ActionButtonState.Idle,
    };

    // ── Inner binding record ─────────────────────────────────────────────────

    private sealed class ButtonBinding
    {
        public ActionButton        Button     { get; }
        public ActionButtonConfig  Config     { get; }
        public int                 StateIndex { get; private set; }

        private readonly Action<float> _oscFeedbackHandler;

        public ButtonBinding(ActionButton button, ActionButtonConfig config, int stateIndex)
        {
            Button     = button;
            Config     = config;
            StateIndex = stateIndex;

            _oscFeedbackHandler = OnOscFeedback;
            if (!string.IsNullOrEmpty(config.OscFeedback))
            {
                OscService.Instance.Subscribe(config.OscFeedback, _oscFeedbackHandler);
            }
        }

        public void UnsubscribeOsc()
        {
            if (!string.IsNullOrEmpty(Config.OscFeedback))
            {
                OscService.Instance.Unsubscribe(Config.OscFeedback, _oscFeedbackHandler);
            }
        }

        private void OnOscFeedback(float value)
        {
            // Marshal to UI thread
            Button.DispatcherQueue.TryEnqueue(() =>
            {
                // Simple binary mapping (active state >= 0.5, else idle)
                int activeIndex = Config.States.Count > 1 ? 1 : 0;
                StateIndex = value >= 0.5f ? activeIndex : 0;
                ApplyState(this);
            });
        }

        public void OnTapped(object sender, RoutedEventArgs e)
        {
            switch (Config.Kind.ToLowerInvariant())
            {
                case "toggle":
                    StateIndex = (StateIndex + 1) % Config.States.Count;
                    ApplyState(this);

                    int activeIndex = Config.States.Count > 1 ? 1 : 0;
                    float val = (StateIndex == activeIndex) ? Config.ValueOn : Config.ValueOff;
                    if (!string.IsNullOrEmpty(Config.OscOut))
                        OscService.Instance.Send(Config.OscOut, val);
                    break;

                case "trigger":
                    _ = FlashActiveAsync();

                    if (!string.IsNullOrEmpty(Config.OscOut))
                        OscService.Instance.Send(Config.OscOut, Config.ValueOn);
                    break;

                // momentary is handled by OnPressed / OnReleased
            }
        }

        public void OnPressed(object sender, RoutedEventArgs e)
        {
            if (!Config.Kind.Equals("momentary", StringComparison.OrdinalIgnoreCase))
                return;

            // Go to the second state (index 1) while held; fall back to index 0 if only one state
            var activeIndex = Config.States.Count > 1 ? 1 : 0;
            StateIndex = activeIndex;
            ApplyState(this);

            if (!string.IsNullOrEmpty(Config.OscOut))
                OscService.Instance.Send(Config.OscOut, Config.ValueOn);
        }

        public void OnReleased(object sender, RoutedEventArgs e)
        {
            if (!Config.Kind.Equals("momentary", StringComparison.OrdinalIgnoreCase))
                return;

            StateIndex = 0;
            ApplyState(this);

            if (!string.IsNullOrEmpty(Config.OscOut))
                OscService.Instance.Send(Config.OscOut, Config.ValueOff);
        }

        private async Task FlashActiveAsync()
        {
            // Find first non-idle state to flash (falls back to index 0 on single-state buttons)
            var flashIndex = 0;
            for (var i = 0; i < Config.States.Count; i++)
            {
                if (!Config.States[i].Name.Equals("idle", StringComparison.OrdinalIgnoreCase))
                {
                    flashIndex = i;
                    break;
                }
            }

            var saved = StateIndex;
            StateIndex = flashIndex;
            ApplyState(this);

            await Task.Delay(200);

            StateIndex = saved;
            ApplyState(this);
        }

        private static void ApplyState(ButtonBinding b)
        {
            var s = b.Config.States[b.StateIndex];
            b.Button.Status = s.Label;
            b.Button.State  = ParseState(s.Name);
        }
    }
}
