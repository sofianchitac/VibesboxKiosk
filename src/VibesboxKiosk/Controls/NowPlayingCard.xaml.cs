using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using VibesboxKiosk.Config;
using VibesboxKiosk.Services;
using Windows.Storage.Streams;
using static VibesboxKiosk.Controls.AnimFactory;

namespace VibesboxKiosk.Controls;

/// <summary>
/// Now Playing card. Layers over the SpectrumView. Three rendered layouts —
/// generic (single big title), centered-text (no artwork), and album-art
/// (artwork + text). Picks one based on payload shape.
///
/// Visibility behaviour:
/// • On NowPlayingService UniqueId change → fade in, start the auto-hide timer
/// • On track ending in <5s (duration known) → fade in again, fire-once per track
/// • On payload cleared → fade out immediately, stop the reshow loop
/// • Tap toggles visibility (also catches taps on the SpectrumView area beneath
///   when the card is hidden, since the card sits at higher z-order in its grid cell)
/// • After every visible→hidden transition while a track is loaded → fade in
///   again after the reshow delay, looping for as long as the track stays loaded.
/// </summary>
public sealed partial class NowPlayingCard : UserControl
{
    private const double TrackEndAlertSec = 5.0;

    private readonly NowPlayingViewModel _vm = NowPlayingService.Instance.ViewModel;
    private readonly DispatcherTimer _hideTimer   = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _reshowTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private NowPlayingConfig _config = new();
    private readonly DispatcherTimer _tickTimer   = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private bool _visible;
    private string? _lastTriggeredUid;
    private bool _trackEndFiredForCurrent;
    private string? _lastDecodedArtworkB64;
    private BitmapImage? _cachedArtwork;
    private bool _isSecondaryPalette;

    public NowPlayingCard()
    {
        InitializeComponent();
        _hideTimer.Tick   += (_, _) => Hide();
        _reshowTimer.Tick += (_, _) => OnReshowTick();
        _tickTimer.Tick   += (_, _) => OnTick();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Configure(NowPlayingConfig config)
    {
        _config = config;
        _hideTimer.Interval   = TimeSpan.FromSeconds(Math.Max(1, config.AutoHideSeconds));
        _reshowTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, config.ReshowSeconds));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged += OnViewModelChanged;
        _tickTimer.Start();
        Render();   // catch a cached event that landed before we loaded
        MaybeTriggerOnInitialState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.PropertyChanged -= OnViewModelChanged;
        _tickTimer.Stop();
        _hideTimer.Stop();
        _reshowTimer.Stop();
    }

    // ── External entry point for spectrum-area taps ──────────────────────

    /// <summary>Show the card if hidden, hide if visible. Called by DashboardPage
    /// when the user taps anywhere on the SpectrumView area while the card is
    /// hidden (the card already handles taps on itself).</summary>
    public void ToggleFromSpectrumTap()
    {
        if (_visible) Hide();
        else if (_vm.HasTrack) Show();
    }

    // ── ViewModel reactions ──────────────────────────────────────────────

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NowPlayingViewModel.HasTrack):
                if (!_vm.HasTrack) { Hide(); _lastTriggeredUid = null; _trackEndFiredForCurrent = false; }
                break;

            case nameof(NowPlayingViewModel.UniqueId):
                // NB: gate on UniqueId presence directly, not HasTrack —
                // NowPlayingService sets HasTrack LAST, so when this fires
                // HasTrack is still the previous value.
                if (!string.IsNullOrEmpty(_vm.UniqueId) && _vm.UniqueId != _lastTriggeredUid)
                {
                    _lastTriggeredUid = _vm.UniqueId;
                    _trackEndFiredForCurrent = false;
                    Render();
                    Show();
                }
                break;

            case nameof(NowPlayingViewModel.Title):
            case nameof(NowPlayingViewModel.Artist):
            case nameof(NowPlayingViewModel.Album):
            case nameof(NowPlayingViewModel.SourceDisplayName):
            case nameof(NowPlayingViewModel.Producer):
            case nameof(NowPlayingViewModel.ArtworkBase64):
            case nameof(NowPlayingViewModel.DurationSeconds):
                Render();
                break;
        }
    }

    private void MaybeTriggerOnInitialState()
    {
        // If a payload was already cached before this control loaded (typical:
        // service connects and gets state immediately; card loads a moment later),
        // count it as a fresh trigger.
        if (!string.IsNullOrEmpty(_vm.UniqueId) && _vm.UniqueId != _lastTriggeredUid)
        {
            _lastTriggeredUid = _vm.UniqueId;
            _trackEndFiredForCurrent = false;
            Show();
        }
    }

    // ── Periodic tick: progress bar + track-end re-trigger ──────────────

    private void OnTick()
    {
        if (!_vm.HasTrack) return;
        double? dur = _vm.DurationSeconds;
        double? el  = _vm.ElapsedSeconds;
        double? cap = _vm.ElapsedCapturedAt;

        if (dur is null or <= 0 || el is null || cap is null) return;

        double nowSec      = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        double rate        = _vm.Playing ? _vm.PlaybackRate : 0.0;
        double liveElapsed = el.Value + (nowSec - cap.Value) * rate;
        if (liveElapsed < 0) liveElapsed = 0;
        if (liveElapsed > dur.Value) liveElapsed = dur.Value;

        double pct = liveElapsed / dur.Value;
        UpdateProgressFill(pct);

        if (!_trackEndFiredForCurrent && liveElapsed >= dur.Value - TrackEndAlertSec)
        {
            _trackEndFiredForCurrent = true;
            Show();
        }
    }

    private void UpdateProgressFill(double pct)
    {
        double cWidth = CenteredProgress.ActualWidth;
        double aWidth = ArtProgress.ActualWidth;
        if (cWidth > 0) CenteredProgressFill.Width = cWidth * pct;
        if (aWidth > 0) ArtProgressFill.Width      = aWidth * pct;
    }

    // ── Render: pick layout + bind values ───────────────────────────────

    private void Render()
    {
        // Gate on Title (not HasTrack), because HasTrack is set LAST by
        // NowPlayingService and intermediate PropertyChanged events would
        // otherwise see HasTrack still false and bail out before content lands.
        if (string.IsNullOrEmpty(_vm.Title))
        {
            GenericTitle.Visibility  = Visibility.Collapsed;
            CenteredStack.Visibility = Visibility.Collapsed;
            ArtLayout.Visibility     = Visibility.Collapsed;
            return;
        }

        ApplyPalette(_vm.Producer);

        string title       = _vm.Title  ?? "";
        string? artist     = NonEmpty(_vm.Artist);
        string? album      = NonEmpty(_vm.Album);
        bool   hasArtwork  = !string.IsNullOrEmpty(_vm.ArtworkBase64);
        string sourceLabel = FormatSourceLabel(_vm.SourceDisplayName);
        bool   hasDuration = _vm.DurationSeconds is > 0;

        if (artist is null && album is null && !hasArtwork)
        {
            // Layout 1: Generic single-title
            GenericTitle.Text       = title;
            GenericTitle.Visibility = Visibility.Visible;
            CenteredStack.Visibility = Visibility.Collapsed;
            ArtLayout.Visibility     = Visibility.Collapsed;
            return;
        }

        GenericTitle.Visibility = Visibility.Collapsed;

        if (hasArtwork)
        {
            // Layout 3: Album art
            ArtLayout.Visibility     = Visibility.Visible;
            CenteredStack.Visibility = Visibility.Collapsed;

            ArtSourceLabel.Text = sourceLabel;
            ArtSourceLabel.Visibility = string.IsNullOrEmpty(sourceLabel) ? Visibility.Collapsed : Visibility.Visible;
            ArtTitle.Text  = title;
            ArtArtist.Text = artist ?? "";
            ArtArtist.Visibility = artist is null ? Visibility.Collapsed : Visibility.Visible;
            ArtAlbum.Text  = album ?? "";
            ArtAlbumRow.Visibility = album is null ? Visibility.Collapsed : Visibility.Visible;
            ArtProgress.Visibility = hasDuration ? Visibility.Visible : Visibility.Collapsed;
            UpdateArtwork();
        }
        else
        {
            // Layout 2: Centered text, no artwork
            CenteredStack.Visibility = Visibility.Visible;
            ArtLayout.Visibility     = Visibility.Collapsed;

            CenteredSourceLabel.Text       = sourceLabel;
            CenteredSourceLabel.Visibility = string.IsNullOrEmpty(sourceLabel) ? Visibility.Collapsed : Visibility.Visible;
            CenteredTitle.Text  = title;
            CenteredArtist.Text = artist ?? "";
            CenteredArtist.Visibility = artist is null ? Visibility.Collapsed : Visibility.Visible;
            CenteredAlbum.Text  = album ?? "";
            CenteredAlbum.Visibility = album is null ? Visibility.Collapsed : Visibility.Visible;
            CenteredProgress.Visibility = hasDuration ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateArtwork()
    {
        // Key the cache off ArtworkBase64 directly, not ArtworkSha256.
        // NowPlayingService.ApplyTrackChange sets ViewModel fields one by one,
        // and each setter fires PropertyChanged synchronously — so a Render
        // triggered by an earlier setter (Title/Artist/Album) sees a transient
        // mix of new-track and previous-track fields. If we compared against
        // ArtworkSha256 (which is set AFTER ArtworkBase64), an intermediate
        // Render could match the previous track's sha and re-use the cached
        // previous bitmap, causing the artwork to lag the title by one track.
        string? b64 = _vm.ArtworkBase64;
        if (string.IsNullOrEmpty(b64)) { AlbumArtImage.Source = null; return; }

        if (b64 == _lastDecodedArtworkB64 && _cachedArtwork is not null)
        {
            AlbumArtImage.Source = _cachedArtwork;
            return;
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(b64);
            var bmp = new BitmapImage();
            using (var stream = new InMemoryRandomAccessStream())
            {
                stream.AsStreamForWrite().Write(bytes, 0, bytes.Length);
                stream.Seek(0);
                bmp.SetSource(stream);
            }
            _cachedArtwork        = bmp;
            _lastDecodedArtworkB64 = b64;
            AlbumArtImage.Source = bmp;
        }
        catch (Exception ex)
        {
            Log.Warn($"NowPlayingCard: artwork decode failed: {ex.Message}");
            AlbumArtImage.Source = null;
        }
    }

    // ── Source-label + palette rules ─────────────────────────────────────

    private void ApplyPalette(string? producer)
    {
        bool secondary = producer is not null && _config.SecondaryColorProducers.Contains(producer);
        if (secondary == _isSecondaryPalette) return;
        _isSecondaryPalette = secondary;

        var accent = (SolidColorBrush)Application.Current.Resources[
            secondary ? "ColorCyanBrush" : "ColorOrangeBrush"];

        CenteredSourceLabel.Foreground = accent;
        CenteredArtist.Foreground      = accent;
        ArtSourceLabel.Foreground      = accent;
        ArtArtist.Foreground           = accent;
        CenteredProgressFill.Fill      = accent;
        ArtProgressFill.Fill           = accent;
    }

    /// <summary>Strips the configured prefix, applies aliases, uppercases.</summary>
    private string FormatSourceLabel(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "";
        string prefix = _config.StripSourcePrefix;
        if (prefix.Length > 0 && displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            displayName = displayName[prefix.Length..];
        foreach (var (from, to) in _config.SourceAliases)
            if (displayName.Equals(from, StringComparison.OrdinalIgnoreCase))
                return to.ToUpperInvariant();
        return displayName.ToUpperInvariant();
    }

    private static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ── Show / Hide animations ───────────────────────────────────────────

    public void Show()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
        _reshowTimer.Stop();
        if (!_visible)
        {
            _visible = true;
            IsHitTestVisible = true;
            AnimateIn();
        }
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _reshowTimer.Stop();
        if (_visible)
        {
            _visible = false;
            IsHitTestVisible = false;
            AnimateOut();
            if (_vm.HasTrack) _reshowTimer.Start();
        }
    }

    private void OnReshowTick()
    {
        _reshowTimer.Stop();
        if (_vm.HasTrack) Show();
    }

    // Entrance: fade in while the content scales up + rises with a gentle overshoot.
    private void AnimateIn()
    {
        var settle = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };
        var sb = new Storyboard();
        sb.Children.Add(Anim(this, "Opacity", null, 1.0, 240, new CubicEase { EasingMode = EasingMode.EaseOut }));
        sb.Children.Add(Anim(ContentTransform, "ScaleX", 0.94, 1.0, 400, settle));
        sb.Children.Add(Anim(ContentTransform, "ScaleY", 0.94, 1.0, 400, settle));
        sb.Children.Add(Anim(ContentTransform, "TranslateY", 14, 0, 400, settle));
        sb.Begin();
    }

    // Exit: fade out while the content recedes slightly back into space.
    private void AnimateOut()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var sb = new Storyboard();
        sb.Children.Add(Anim(this, "Opacity", null, 0.0, 300, ease));
        sb.Children.Add(Anim(ContentTransform, "ScaleX", null, 0.97, 300, ease));
        sb.Children.Add(Anim(ContentTransform, "ScaleY", null, 0.97, 300, ease));
        sb.Children.Add(Anim(ContentTransform, "TranslateY", null, 8, 300, ease));
        sb.Begin();
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        // Tap while visible → hide. (Spectrum-area taps when hidden are
        // routed in via ToggleFromSpectrumTap() from the page.)
        if (_visible) Hide();
        e.Handled = true;
    }
}
