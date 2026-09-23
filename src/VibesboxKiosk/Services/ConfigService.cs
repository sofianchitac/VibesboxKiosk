using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using VibesboxKiosk.Config;

namespace VibesboxKiosk.Services;

/// <summary>
/// Loads, saves and watches kiosk-config.json.
///
/// Location: a kiosk-config.json next to the exe wins (portable install);
/// otherwise %LOCALAPPDATA%\VibesboxKiosk\kiosk-config.json, created from
/// <see cref="DefaultConfig"/> on first run.
/// </summary>
public sealed class ConfigService
{
    public static ConfigService Instance { get; } = new();

    private const string FileName = "kiosk-config.json";

    private static readonly HashSet<string> ValidStateNames =
        new(StringComparer.OrdinalIgnoreCase) { "idle", "playing", "active", "off" };

    private static readonly KioskConfig DefaultConfigValues = new();

    // A user-typed format string must never throw at dashboard build time.
    private static string ValidFormat(string format, object sample, string fallback)
    {
        try
        {
            string.Format(System.Globalization.CultureInfo.InvariantCulture, format, sample);
            return format;
        }
        catch (FormatException)
        {
            Log.Warn($"[Config] Invalid format '{format}' — using '{fallback}'.");
            return fallback;
        }
    }

    private static readonly HashSet<string> ValidKinds =
        new(StringComparer.OrdinalIgnoreCase) { "toggle", "momentary", "trigger" };

    public KioskConfig Current { get; private set; } = DefaultConfig.Create();

    public string ConfigPath { get; } = ResolvePath();

    /// <summary>Raised (on a worker thread) after the file changed on disk or Save() ran.</summary>
    public event EventHandler? ConfigReloaded;

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private string? _lastText;

    private ConfigService() { }

    private static string ResolvePath()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, FileName);
        if (File.Exists(portable)) return portable;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VibesboxKiosk", FileName);
    }

    /// <summary>Loads the file; creates it from the defaults if missing. Keeps the
    /// previous config when the file is malformed. Returns false if nothing changed.</summary>
    public bool Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Log.Info($"[Config] No config at {ConfigPath} — writing defaults.");
                Save(DefaultConfig.Create(), raiseEvent: false);
                return true;
            }

            var text = File.ReadAllText(ConfigPath);
            if (text == _lastText) return false;

            var config = JsonSerializer.Deserialize(text, KioskConfigJsonContext.Default.KioskConfig)
                         ?? throw new InvalidDataException("empty document");

            // A version-1 file (actionButtons / statusFeedback / indicators) would
            // otherwise load as a near-empty layout — and a later Save would erase it.
            if (config.Version < 2)
            {
                var backup = Path.ChangeExtension(ConfigPath, ".v1.json");
                if (!File.Exists(backup)) File.Copy(ConfigPath, backup);
                throw new InvalidDataException(
                    $"this is a version-1 config (copied to {backup}); the current format is version 2 — recreate it in the settings");
            }

            Normalize(config);
            Current   = config;
            _lastText = text;
            Log.Info($"[Config] Loaded {ConfigPath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[Config] Load error — {ex.Message}. Keeping the previous config.");
            return false;
        }
    }

    /// <summary>Writes atomically (temp file + replace) and applies immediately.</summary>
    public void Save(KioskConfig config, bool raiseEvent = true)
    {
        Normalize(config);
        var text = JsonSerializer.Serialize(config, KioskConfigJsonContext.Default.KioskConfig);

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, ConfigPath, overwrite: true);

        Current   = config;
        _lastText = text;
        Log.Info("[Config] Saved.");
        if (raiseEvent) ConfigReloaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Deep copy for editing, so a cancelled edit leaves Current untouched.</summary>
    public KioskConfig CloneCurrent()
    {
        var json = JsonSerializer.Serialize(Current, KioskConfigJsonContext.Default.KioskConfig);
        return JsonSerializer.Deserialize(json, KioskConfigJsonContext.Default.KioskConfig)!;
    }

    public void StartWatching()
    {
        if (_watcher is not null) return;

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(ConfigPath)!, FileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };

        // Atomic saves (temp + rename) raise Created/Renamed rather than Changed.
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += (s, e) => OnFileChanged(s, e);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: editors fire several events per save.
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            if (Load()) ConfigReloaded?.Invoke(this, EventArgs.Empty);
        }, null, 500, Timeout.Infinite);
    }

    // Repairs what the UI can't render instead of rejecting the whole file.
    private static void Normalize(KioskConfig cfg)
    {
        var d = DefaultConfigValues;
        cfg.Spectrum.AddressPattern    = ValidFormat(cfg.Spectrum.AddressPattern, 1, d.Spectrum.AddressPattern);
        cfg.InputMeters.AddressPattern = ValidFormat(cfg.InputMeters.AddressPattern, 1, d.InputMeters.AddressPattern);
        foreach (var o in cfg.Overlays) o.Format = ValidFormat(o.Format, 1.5f, "{0:0}");
        foreach (var s in cfg.Stats)    s.Format = ValidFormat(s.Format, 1.5f, "{0:0}");

        cfg.Display.UiScale = Math.Clamp(cfg.Display.UiScale, 0.5, 3.0);
        cfg.Buttons.Columns = Math.Clamp(cfg.Buttons.Columns, 1, 8);
        cfg.Spectrum.BandCount = Math.Clamp(cfg.Spectrum.BandCount, 2, 512);
        if (cfg.Spectrum.MinHz <= 0) cfg.Spectrum.MinHz = 10;
        if (cfg.Spectrum.MaxHz <= cfg.Spectrum.MinHz) cfg.Spectrum.MaxHz = cfg.Spectrum.MinHz * 1000;
        if (cfg.Spectrum.FloorDb >= 0) cfg.Spectrum.FloorDb = -70;
        cfg.InputMeters.Count = Math.Clamp(cfg.InputMeters.Count, 0, 64);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < cfg.Buttons.Items.Count; i++)
        {
            var b = cfg.Buttons.Items[i];
            if (string.IsNullOrWhiteSpace(b.Id) || !ids.Add(b.Id))
            {
                b.Id = $"btn-{i + 1:00}";
                while (!ids.Add(b.Id)) b.Id += "x";
            }
            if (!ValidKinds.Contains(b.Kind)) b.Kind = "toggle";
            if (b.States.Count == 0) b.States.Add(new ButtonStateConfig());
            foreach (var s in b.States)
                if (!ValidStateNames.Contains(s.Name)) s.Name = "idle";
        }
    }
}
