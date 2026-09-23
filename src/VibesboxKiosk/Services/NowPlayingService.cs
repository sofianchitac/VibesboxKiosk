using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.UI.Dispatching;

namespace VibesboxKiosk.Services;

/// <summary>
/// Subscribes to a Now Playing WebSocket feed (protocol: docs/now-playing.md)
/// and exposes an observable ViewModel for the NowPlayingCard.
/// Connection/reconnect plumbing lives in <see cref="WsSubscriber"/>.
/// </summary>
public sealed class NowPlayingService : WsSubscriber
{
    public static NowPlayingService Instance { get; } = new();

    private DispatcherQueue? _dispatcher;

    public NowPlayingViewModel ViewModel { get; } = new();

    private NowPlayingService() { }

    protected override string Name => "NowPlaying";

    public void Start(DispatcherQueue dispatcher, string wsUrl)
    {
        _dispatcher = dispatcher;
        StartLoop(wsUrl);
    }

    public void Stop() => StopLoop();

    /// <summary>Forget the last track (e.g. when the feed is switched off).</summary>
    public void Clear() => ApplyCleared();

    protected override void OnConnecting()   => SetStatus(NowPlayingConnectionStatus.Connecting);
    protected override void OnConnected()    => SetStatus(NowPlayingConnectionStatus.Connected);
    protected override void OnDisconnected() => SetStatus(NowPlayingConnectionStatus.Disconnected);

    // ── payload → ViewModel ──────────────────────────────────────────────

    protected override void HandleFrame(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return;

        string? @event = TryString(root, "event");
        if (@event is null) return;
        int schema = TryInt(root, "schema") ?? 0;
        if (schema != 1) return;

        switch (@event)
        {
            case "track_change":  ApplyTrackChange(root);  break;
            case "state_change":  ApplyStateChange(root);  break;
            case "cleared":       ApplyCleared();          break;
        }
    }

    private void ApplyTrackChange(JsonElement root)
    {
        string? producer = TryString(root, "producer");
        bool playing     = TryBool(root, "playing") ?? true;
        double rate      = TryDouble(root, "playback_rate") ?? (playing ? 1.0 : 0.0);

        string? title = null, artist = null, album = null, uid = null;
        double? duration = null, elapsed = null;
        if (root.TryGetProperty("track", out var track) && track.ValueKind == JsonValueKind.Object)
        {
            title     = TryString(track, "title");
            artist    = TryString(track, "artist");
            album     = TryString(track, "album");
            uid       = TryString(track, "unique_id");
            duration  = TryDouble(track, "duration_seconds");
            elapsed   = TryDouble(track, "elapsed_seconds");
        }

        // The feed's `elapsed_captured_at` is a Unix timestamp on the sender's clock.
        // OnTick extrapolates with `DateTimeOffset.UtcNow` (kiosk clock), so
        // any drift between the two hosts shows up as a constant offset in
        // the displayed elapsed (~13s skew observed in the field stalls the
        // progress bar at 0 until the kiosk clock catches up, and breaks the
        // <5s track-end re-trigger). Re-stamp with the kiosk's own UTC at
        // message receipt so all math from here on uses a single clock. The
        // tiny network delay (~tens of ms) is absorbed into the displayed
        // elapsed and is below human perception for a progress bar.
        double elapsedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        string? sourceDisplay = null;
        if (root.TryGetProperty("source_app", out var srcApp) && srcApp.ValueKind == JsonValueKind.Object)
            sourceDisplay = TryString(srcApp, "display_name");

        string? artworkB64 = null, artworkMime = null, artworkSha = null;
        if (root.TryGetProperty("artwork", out var art) && art.ValueKind == JsonValueKind.Object)
        {
            artworkB64  = TryString(art, "data_base64");
            artworkMime = TryString(art, "mime");
            artworkSha  = TryString(art, "sha256");
        }

        string? config = null;
        int?    sampleRate = null, channels = null;
        if (root.TryGetProperty("transport", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            sampleRate = TryInt(tr, "sample_rate");
            channels   = TryInt(tr, "channels");
            config     = TryString(tr, "config");
        }

        OnUi(() =>
        {
            var vm = ViewModel;
            vm.Producer          = producer;
            vm.Playing           = playing;
            vm.PlaybackRate      = rate;
            vm.Title             = title;
            vm.Artist            = artist;
            vm.Album             = album;
            vm.UniqueId          = uid;
            vm.SourceDisplayName = sourceDisplay;
            vm.DurationSeconds   = duration;
            vm.ElapsedSeconds    = elapsed;
            vm.ElapsedCapturedAt = elapsedAt;
            vm.ArtworkBase64     = artworkB64;
            vm.ArtworkMime       = artworkMime;
            vm.ArtworkSha256     = artworkSha;
            vm.TransportSampleRate = sampleRate;
            vm.TransportChannels   = channels;
            vm.TransportConfig     = config;
            vm.HasTrack            = !string.IsNullOrEmpty(title);
            vm.LastUpdatedUtc      = DateTime.UtcNow;
        });
    }

    private void ApplyStateChange(JsonElement root)
    {
        bool playing  = TryBool(root, "playing") ?? true;
        double rate   = TryDouble(root, "playback_rate") ?? (playing ? 1.0 : 0.0);
        double? el    = TryDouble(root, "elapsed_seconds");
        // Same single-clock rule as ApplyTrackChange — ignore the sender's
        // `elapsed_captured_at`, stamp the kiosk's own UTC at receipt.
        double elAtLocal = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        OnUi(() =>
        {
            ViewModel.Playing           = playing;
            ViewModel.PlaybackRate      = rate;
            ViewModel.ElapsedSeconds    = el ?? ViewModel.ElapsedSeconds;
            ViewModel.ElapsedCapturedAt = el is null ? ViewModel.ElapsedCapturedAt : elAtLocal;
            ViewModel.LastUpdatedUtc    = DateTime.UtcNow;
        });
    }

    private void ApplyCleared()
    {
        OnUi(() =>
        {
            var vm = ViewModel;
            vm.HasTrack = false;
            vm.Playing  = false;
            vm.Title = vm.Artist = vm.Album = null;
            vm.UniqueId = null;
            vm.DurationSeconds = null;
            vm.ElapsedSeconds = null;
            vm.ElapsedCapturedAt = null;
            vm.ArtworkBase64 = vm.ArtworkMime = vm.ArtworkSha256 = null;
            vm.LastUpdatedUtc = DateTime.UtcNow;
        });
    }

    private void SetStatus(NowPlayingConnectionStatus status)
    {
        OnUi(() => ViewModel.ConnectionStatus = status);
    }

    private void OnUi(Action a)
    {
        if (_dispatcher is null) { a(); return; }
        _dispatcher.TryEnqueue(() => a());
    }

    // ── JSON helpers ─────────────────────────────────────────────────────

    private static string? TryString(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static double? TryDouble(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble() : null;

    private static int? TryInt(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : null;

    private static bool? TryBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True  => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}

public enum NowPlayingConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>
/// Observable model bound by the NowPlayingCard. Property setters
/// raise PropertyChanged so XAML can {x:Bind Mode=OneWay} all of these.
/// </summary>
public sealed class NowPlayingViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private NowPlayingConnectionStatus _connectionStatus;
    public NowPlayingConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        set => Set(ref _connectionStatus, value);
    }

    private bool _hasTrack;
    public bool HasTrack { get => _hasTrack; set => Set(ref _hasTrack, value); }

    private string? _producer;
    public string? Producer { get => _producer; set => Set(ref _producer, value); }

    private bool _playing;
    public bool Playing { get => _playing; set => Set(ref _playing, value); }

    private double _playbackRate;
    public double PlaybackRate { get => _playbackRate; set => Set(ref _playbackRate, value); }

    private string? _title;
    public string? Title { get => _title; set => Set(ref _title, value); }

    private string? _artist;
    public string? Artist { get => _artist; set => Set(ref _artist, value); }

    private string? _album;
    public string? Album { get => _album; set => Set(ref _album, value); }

    private string? _uniqueId;
    public string? UniqueId { get => _uniqueId; set => Set(ref _uniqueId, value); }

    private string? _sourceDisplayName;
    public string? SourceDisplayName { get => _sourceDisplayName; set => Set(ref _sourceDisplayName, value); }

    private double? _durationSeconds;
    public double? DurationSeconds { get => _durationSeconds; set => Set(ref _durationSeconds, value); }

    private double? _elapsedSeconds;
    public double? ElapsedSeconds { get => _elapsedSeconds; set => Set(ref _elapsedSeconds, value); }

    private double? _elapsedCapturedAt;
    public double? ElapsedCapturedAt { get => _elapsedCapturedAt; set => Set(ref _elapsedCapturedAt, value); }

    private string? _artworkBase64;
    public string? ArtworkBase64 { get => _artworkBase64; set => Set(ref _artworkBase64, value); }

    private string? _artworkMime;
    public string? ArtworkMime { get => _artworkMime; set => Set(ref _artworkMime, value); }

    private string? _artworkSha256;
    public string? ArtworkSha256 { get => _artworkSha256; set => Set(ref _artworkSha256, value); }

    private int? _transportSampleRate;
    public int? TransportSampleRate { get => _transportSampleRate; set => Set(ref _transportSampleRate, value); }

    private int? _transportChannels;
    public int? TransportChannels { get => _transportChannels; set => Set(ref _transportChannels, value); }

    private string? _transportConfig;
    public string? TransportConfig { get => _transportConfig; set => Set(ref _transportConfig, value); }

    private DateTime? _lastUpdatedUtc;
    public DateTime? LastUpdatedUtc { get => _lastUpdatedUtc; set => Set(ref _lastUpdatedUtc, value); }

    private void Set<T>(ref T storage, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
