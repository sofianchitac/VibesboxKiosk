using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using VibesboxKiosk.Config;
using VibesboxKiosk.Controls;
using VibesboxKiosk.Services;
using Windows.UI;

namespace VibesboxKiosk.Pages;

/// <summary>
/// The kiosk screen. Built entirely from the current config: MainWindow creates a
/// fresh page after every config change, so nothing here handles a reload.
/// </summary>
public sealed partial class DashboardPage : Page
{
    private const double DesignWidth  = 1024;
    private const double DesignHeight = 768;

    private readonly DispatcherQueue _dispatcher;
    private readonly KioskConfig _config;
    private readonly List<(FrameworkElement Element, TranslateTransform Transform)> _animated = new();
    private readonly List<StatBinding> _stats = new();
    private DispatcherQueueTimer? _statWatchdog;

    /// <summary>Raised when the user asks for the settings page.</summary>
    public event EventHandler? SettingsRequested;

    public DashboardPage()
    {
        InitializeComponent();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _config     = ConfigService.Instance.Current;

        BuildFromConfig();

        HeaderLogo.Toggled += OnHeaderLogoToggled;
        OverlayService.Instance.HudRequested   += OnHudRequested;
        OverlayService.Instance.PowerRequested += OnPowerRequested;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Log.Info(
            $"[Startup] Dashboard loaded {App.StartupStopwatch.ElapsedMilliseconds} ms after process start");
        StartServices();
        PlayWakeUpAnimation();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        OverlayService.Instance.HudRequested   -= OnHudRequested;
        OverlayService.Instance.PowerRequested -= OnPowerRequested;
        _statWatchdog?.Stop();
        _glowColorSb?.Stop();
        _glowSweepSb?.Stop();
    }

    // ── Scaling ──────────────────────────────────────────────────────────────

    // Fit the 1024×768 design into the window, then grow the virtual canvas along
    // the longer screen axis so the Viewbox fills it exactly.
    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double w = e.NewSize.Width, h = e.NewSize.Height;
        if (w <= 0 || h <= 0) return;

        double scale = Math.Min(w / DesignWidth, h / DesignHeight) * _config.Display.UiScale;
        RootGrid.Width  = w / scale;
        RootGrid.Height = h / scale;
    }

    // ── Build ────────────────────────────────────────────────────────────────

    private void BuildFromConfig()
    {
        HeaderLogo.Configure(_config.Theme);
        ApplyThemeToXamlStops();

        SpectrumView.Visibility = _config.Spectrum.Enabled ? Visibility.Visible : Visibility.Collapsed;
        SpectrumView.Configure(_config.Spectrum.MinHz, _config.Spectrum.MaxHz, _config.Spectrum.FloorDb, _config.Spectrum.GridLines);
        NowPlayingCard.Configure(_config.NowPlaying);

        foreach (var stat in _config.Stats)
        {
            var pair = new StatPair { Label = stat.Label, Unit = stat.Unit, Value = "–" };
            StatsRow.Children.Add(pair);
            _stats.Add(new StatBinding(stat, pair));
        }

        BuildButtonRows();

        foreach (var a in _config.Activity)
        {
            ActivityRows.Children.Add(new IndicatorDotRow
            {
                GroupLabel = a.Label,
                DotCount   = 1,
                DotSize    = 16,
            });
        }

        var meters = _config.InputMeters;
        InputDots.Configure(meters.Enabled ? meters.Count : 0, meters.Groups);

        var power = _config.Power;
        PowerOverlayControl.Configure(power.Shutdown, power.Restart, power.DisplayOff);
        PowerBtn.Visibility = power.Shutdown || power.Restart || power.DisplayOff
            ? Visibility.Visible : Visibility.Collapsed;

        _animated.Add((HeaderLogo,   HeaderLogoTransform));
        _animated.Add((SpectrumArea, SpectrumTransform));
        _animated.Add((StatsRow,     StatsTransform));
        foreach (FrameworkElement row in ButtonRows.Children)
            _animated.Add((row, (TranslateTransform)row.RenderTransform));
        _animated.Add((IndicatorsFooter, IndicatorsTransform));
    }

    private readonly Dictionary<string, ActionButton> _buttons = new();

    private void BuildButtonRows()
    {
        var items   = _config.Buttons.Items;
        int columns = _config.Buttons.Columns;

        for (int start = 0; start < items.Count; start += columns)
        {
            var row = new Grid
            {
                ColumnSpacing = 24,
                Opacity = 0,
                RenderTransform = new TranslateTransform { Y = 20 },
            };
            for (int c = 0; c < columns; c++)
                row.ColumnDefinitions.Add(new ColumnDefinition());

            for (int c = 0; c < columns && start + c < items.Count; c++)
            {
                var cfg = items[start + c];
                var btn = new ActionButton { ButtonId = cfg.Id, Label = cfg.Name };
                Grid.SetColumn(btn, c);
                row.Children.Add(btn);
                _buttons[cfg.Id] = btn;
            }
            ButtonRows.Children.Add(row);
        }
    }

    // Gradient stops declared in XAML without colours (edge fades, glow bands).
    private void ApplyThemeToXamlStops()
    {
        byte[] fadeAlphas = { 0xFF, 0xF2, 0xB3, 0x5C, 0x1A, 0x00 };
        foreach (var brush in new[] { LeftEdgeFade, RightEdgeFade })
            for (int i = 0; i < brush.GradientStops.Count; i++)
                brush.GradientStops[i].Color = ThemeService.WithAlpha(ThemeService.Background, fadeAlphas[i]);

        foreach (var s in new[] { TopSolid, BottomSolid, LeftSolid, RightSolid })
            s.Color = ThemeService.AccentSoft;
        foreach (var s in new[] { TopFade, BottomFade, LeftFade, RightFade })
            s.Color = ThemeService.WithAlpha(ThemeService.AccentSoft, 0);
    }

    // ── Services ─────────────────────────────────────────────────────────────

    private void StartServices()
    {
        OscService.Instance.Start(_config.Osc);

        SpectrumService.Instance.Start(_config.Spectrum, SpectrumView, _dispatcher);
        var activityRows = new List<IndicatorDotRow>();
        foreach (IndicatorDotRow r in ActivityRows.Children) activityRows.Add(r);
        IndicatorService.Instance.Start(_config, InputDots, activityRows, _dispatcher);
        OverlayService.Instance.Start(_config.Overlays, _dispatcher);

        if (_config.NowPlaying.Enabled && _config.NowPlaying.Url.Length > 0)
            NowPlayingService.Instance.Start(_dispatcher, _config.NowPlaying.Url);
        else
            NowPlayingService.Instance.Clear();

        AutomationService.Start(_config.Automations);

        ButtonBindingService.Instance.Bind(_buttons, _config.Buttons.Items);
        WireStats();
        WireHeader();

        // Ask the host to push the current state of every feedback address.
        if (_config.Osc.RefreshAddress.Length > 0)
            OscService.Instance.Send(_config.Osc.RefreshAddress, 1.0f);
    }

    // ── Stats ────────────────────────────────────────────────────────────────

    private sealed class StatBinding
    {
        public StatConfig Config { get; }
        public StatPair Pair { get; }
        public long LastTicks;

        public StatBinding(StatConfig config, StatPair pair) { Config = config; Pair = pair; }

        public void Show(float v)
        {
            LastTicks = Environment.TickCount64;
            Pair.Value = Config.OnOff
                ? (v >= 0.5f ? Config.OnText : Config.OffText)
                : ValueFormat.Apply(Config.Format, v);
        }

        public void CheckTimeout(long now)
        {
            if (Config.TimeoutMs > 0 && now - LastTicks > Config.TimeoutMs)
                Pair.Value = Config.OnOff ? Config.OffText : "–";
        }
    }

    private void WireStats()
    {
        foreach (var stat in _stats)
        {
            if (stat.Config.Address.Length == 0) continue;
            var s = stat;
            OscService.Instance.Subscribe(s.Config.Address, v => _dispatcher.TryEnqueue(() => s.Show(v)));
        }

        _statWatchdog = _dispatcher.CreateTimer();
        _statWatchdog.Interval = TimeSpan.FromMilliseconds(500);
        _statWatchdog.Tick += (_, _) =>
        {
            long now = Environment.TickCount64;
            foreach (var s in _stats) s.CheckTimeout(now);
        };
        _statWatchdog.Start();
    }

    // ── Header toggle + edge glow ────────────────────────────────────────────

    private bool _masterGlowEnabled;
    private float _lastMasterVolume;

    private void WireHeader()
    {
        var h = _config.Header;

        // Feedback in case the toggle is changed elsewhere.
        if (h.ToggleAddress.Length > 0)
            OscService.Instance.Subscribe(h.ToggleAddress, val => _dispatcher.TryEnqueue(() =>
            {
                SetToggle(val >= 0.5f);
                HeaderLogo.SetColor(_masterGlowEnabled);
            }));

        if (h.GlowLevelAddress.Length > 0)
            OscService.Instance.Subscribe(h.GlowLevelAddress, val => _dispatcher.TryEnqueue(() =>
            {
                _lastMasterVolume = val;
                if (_masterGlowEnabled) ApplyMasterGlow();
            }));
    }

    private void OnHeaderLogoToggled(object? sender, bool on)
    {
        SetToggle(on);
        var h = _config.Header;
        if (h.ToggleAddress.Length > 0)
            OscService.Instance.Send(h.ToggleAddress, on ? 1.0f : 0.0f);
        WsCommand.Send(h.WebSocketUrl, on ? h.WebSocketOn : h.WebSocketOff);
    }

    private void SetToggle(bool on)
    {
        _masterGlowEnabled = on && _config.Header.Glow;
        UpdatePartyGlow();
        ApplyMasterGlow();
    }

    private void HeaderLogo_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        e.Handled = true;
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyMasterGlow()
    {
        MasterGlow.Opacity = _masterGlowEnabled ? Math.Clamp(_lastMasterVolume, 0, 1) : 0;
    }

    // While the toggle is on, two looping storyboards run on top of the level-driven
    // opacity: a slow colour cycle through the theme, and an out-of-phase brightness
    // + inward-bloom sweep between the horizontal and vertical edge pairs.

    private Storyboard? _glowColorSb;
    private Storyboard? _glowSweepSb;
    private bool _partyGlowRunning;

    private void UpdatePartyGlow()
    {
        if (_glowColorSb is null) BuildPartyGlow();
        if (_masterGlowEnabled == _partyGlowRunning) return;   // ignore OSC echo / re-entry
        _partyGlowRunning = _masterGlowEnabled;

        if (_masterGlowEnabled)
        {
            _glowColorSb!.Begin();
            _glowSweepSb!.Begin();
        }
        else
        {
            // Stop() reverts colours + pair opacity/scale to their base values.
            _glowColorSb!.Stop();
            _glowSweepSb!.Stop();
        }
    }

    private void BuildPartyGlow()
    {
        Color[] palette = { ThemeService.AccentSoft, ThemeService.Accent, ThemeService.Muted, ThemeService.Text };
        var solids = new[] { TopSolid, BottomSolid, LeftSolid, RightSolid };
        var fades  = new[] { TopFade,  BottomFade,  LeftFade,  RightFade  };

        _glowColorSb = new Storyboard();
        foreach (var s in solids) _glowColorSb.Children.Add(BuildColorCycle(s, palette, 0xFF));
        foreach (var f in fades)  _glowColorSb.Children.Add(BuildColorCycle(f, palette, 0x00));

        // Horizontal pair starts bright + bloomed, vertical starts dim + flat — 180°
        // apart, so emphasis sweeps diagonally between the framings.
        _glowSweepSb = new Storyboard();
        _glowSweepSb.Children.Add(BuildSweep(GlowHorizontal, "Opacity", 1.0,  0.35));
        _glowSweepSb.Children.Add(BuildSweep(TopScale,       "ScaleY",  1.5,  1.0));
        _glowSweepSb.Children.Add(BuildSweep(BottomScale,    "ScaleY",  1.5,  1.0));
        _glowSweepSb.Children.Add(BuildSweep(GlowVertical,   "Opacity", 0.35, 1.0));
        _glowSweepSb.Children.Add(BuildSweep(LeftScale,      "ScaleX",  1.0,  1.5));
        _glowSweepSb.Children.Add(BuildSweep(RightScale,     "ScaleX",  1.0,  1.5));
    }

    private static ColorAnimationUsingKeyFrames BuildColorCycle(GradientStop stop, Color[] palette, byte alpha)
    {
        const double seg = 3.5; // seconds per colour
        // Gradient-stop colour is a dependent (non-composition) animation — WinUI
        // silently drops it unless this is set.
        var a = new ColorAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true,
        };
        for (int i = 0; i <= palette.Length; i++)   // wrap back to the first colour
        {
            a.KeyFrames.Add(new LinearColorKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seg * i)),
                Value   = ThemeService.WithAlpha(palette[i % palette.Length], alpha),
            });
        }
        Storyboard.SetTarget(a, stop);
        Storyboard.SetTargetProperty(a, "Color");
        return a;
    }

    private static DoubleAnimation BuildSweep(DependencyObject target, string property, double from, double to)
    {
        var a = new DoubleAnimation
        {
            From           = from,
            To             = to,
            Duration       = TimeSpan.FromMilliseconds(9000),
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(a, target);
        Storyboard.SetTargetProperty(a, property);
        return a;
    }

    // ── Wake-up animation ────────────────────────────────────────────────────

    private void PlayWakeUpAnimation()
    {
        for (int i = 0; i < _animated.Count; i++)
            _ = AnimateElementIn(_animated[i].Element, _animated[i].Transform, i * 100);
    }

    private static async Task AnimateElementIn(FrameworkElement element, TranslateTransform transform, int delayMs)
    {
        await Task.Delay(delayMs);

        var ease = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut };
        var dur = TimeSpan.FromMilliseconds(600);

        var sb = new Storyboard();
        sb.Children.Add(BuildAnim(element,   "Opacity", to: 1.0, dur, ease));
        sb.Children.Add(BuildAnim(transform, "Y",       to: 0,   dur, ease));
        sb.Begin();
    }

    private static DoubleAnimation BuildAnim(DependencyObject target, string property, double to, TimeSpan duration, EasingFunctionBase? easing = null)
    {
        var anim = new DoubleAnimation { To = to, Duration = duration, EasingFunction = easing };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, property);
        return anim;
    }

    // ── Spectrum-area tap: show / hide Now Playing card ──────────────────────

    private void SpectrumArea_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // When the card is visible it handles its own tap (and sets Handled).
        // This handler only fires for taps that fell through to the spectrum.
        NowPlayingCard.ToggleFromSpectrumTap();
        e.Handled = true;
    }

    // ── Overlays ─────────────────────────────────────────────────────────────

    private void OnHudRequested(object? sender, HudRequestedEventArgs e) => HudOverlay.Show(e.Label, e.Value);

    private void OnPowerRequested(object? sender, EventArgs e) => PowerOverlayControl.Show();

    private void PowerBtn_Click(object sender, RoutedEventArgs e) => OverlayService.Instance.ShowPower();

    private async void PowerOverlay_ShutdownClicked(object? sender, EventArgs e)
    {
        PowerOverlayControl.Hide();
        SendBeforeShutdown();
        await PlayShutdownSequence("SHUTTING DOWN");
        PowerService.Shutdown();
    }

    private async void PowerOverlay_RestartClicked(object? sender, EventArgs e)
    {
        PowerOverlayControl.Hide();
        SendBeforeShutdown();
        await PlayShutdownSequence("RESTARTING");
        PowerService.Restart();
    }

    // Sent ahead of the shutdown animation so the host (e.g. a DAW) can save and
    // quit during the grace period instead of blocking Windows shutdown with a
    // "save changes?" dialog.
    private void SendBeforeShutdown()
    {
        var addr = _config.Power.BeforeShutdownAddress;
        if (addr.Length > 0) OscService.Instance.Send(addr, 1.0f);
    }

    // Fade in the shutdown message, play the BlackHole collapse on the dashboard,
    // wait the grace period, then return. PowerService fires immediately after.
    private async Task PlayShutdownSequence(string label)
    {
        ShutdownOverlayControl.Show(label);
        PlayBlackHole(DashboardContent);
        await Task.Delay(1200);
    }

    private void PlayBlackHole(FrameworkElement target)
    {
        var collapse = new ExponentialEase { Exponent = 7, EasingMode = EasingMode.EaseIn };
        var scaleDur = TimeSpan.FromMilliseconds(1500);

        var sb = new Storyboard();
        sb.Children.Add(BuildAnim(DashboardScale, "ScaleX", to: 0.8, scaleDur, collapse));
        sb.Children.Add(BuildAnim(DashboardScale, "ScaleY", to: 0.8, scaleDur, collapse));
        sb.Children.Add(BuildAnim(target,         "Opacity", to: 0.0, TimeSpan.FromMilliseconds(1200)));
        sb.Begin();
    }

    private async void PowerOverlay_DisplayOffClicked(object? sender, EventArgs e)
    {
        // Let the power overlay's 200ms fade finish compositing first: powering the
        // panel off mid-fade left a band of the old frame on screen after wake.
        await Task.Delay(400);
        DisplaySleepService.TurnOffDisplay();
    }
}
