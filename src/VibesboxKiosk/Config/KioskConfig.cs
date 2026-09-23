using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VibesboxKiosk.Config;

// The whole user configuration, persisted as kiosk-config.json. Every class has
// defaults, so a partial file (or an older one) still loads, and the settings
// page can edit a deep copy and save it back.

public sealed class KioskConfig
{
    public int Version { get; set; } = 2;
    public DisplayConfig Display { get; set; } = new();
    public OscConfig Osc { get; set; } = new();
    public ThemeConfig Theme { get; set; } = new();
    public HeaderConfig Header { get; set; } = new();
    public SpectrumConfig Spectrum { get; set; } = new();
    public List<StatConfig> Stats { get; set; } = new();
    public ButtonsConfig Buttons { get; set; } = new();
    public List<OverlayConfig> Overlays { get; set; } = new();
    public List<ActivityConfig> Activity { get; set; } = new();
    public InputMetersConfig InputMeters { get; set; } = new();
    public NowPlayingConfig NowPlaying { get; set; } = new();
    public List<AutomationConfig> Automations { get; set; } = new();
    public PowerConfig Power { get; set; } = new();
}

public sealed class DisplayConfig
{
    public bool Fullscreen { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    /// <summary>0 = primary display; 1.. = the other displays in enumeration order.</summary>
    public int Monitor { get; set; }
    /// <summary>Multiplier on top of the automatic fit-to-screen scale.</summary>
    public double UiScale { get; set; } = 1.0;
}

public sealed class OscConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int SendPort { get; set; } = 8000;
    public int ListenPort { get; set; } = 9000;
    /// <summary>Sent once on start so the host can push every feedback value.</summary>
    public string RefreshAddress { get; set; } = "/vibesbox/dsp/refresh";
}

public sealed class ThemeConfig
{
    public string Background { get; set; } = "#000000";
    public string Surface { get; set; } = "#141414";
    public string Text { get; set; } = "#FFFFFF";
    public string Muted { get; set; } = "#8B796B";
    public string Accent { get; set; } = "#FF7049";
    public string AccentSoft { get; set; } = "#FDA564";
    public string Secondary { get; set; } = "#59C2EB";
    /// <summary>Image file (SVG/PNG/JPG); empty = built-in logo.</summary>
    public string Logo { get; set; } = "";
    /// <summary>Shown while the logo toggle is on; empty = built-in (or Logo, if set).</summary>
    public string LogoActive { get; set; } = "";
    /// <summary>Logo height in design pixels (the dashboard is laid out on a 1024×768 grid).</summary>
    public double LogoHeight { get; set; } = 22;
}

public sealed class HeaderConfig
{
    /// <summary>Tapping the logo toggles this (1/0); feedback on the same address.</summary>
    public string ToggleAddress { get; set; } = "/vibesbox/glow/enable";
    /// <summary>While the toggle is on, the screen edges glow with this 0..1 level.</summary>
    public bool Glow { get; set; } = true;
    public string GlowLevelAddress { get; set; } = "/vibesbox/master/vol";
    /// <summary>Optional WebSocket that also receives the toggle.</summary>
    public string WebSocketUrl { get; set; } = "";
    public string WebSocketOn { get; set; } = "";
    public string WebSocketOff { get; set; } = "";
}

public sealed class SpectrumConfig
{
    public bool Enabled { get; set; } = true;
    public int BandCount { get; set; } = 64;
    /// <summary>{0} is replaced by the band index (0-based).</summary>
    public string AddressPattern { get; set; } = "/vibesbox/spectrum/{0}";
    public double MinHz { get; set; } = 10;
    public double MaxHz { get; set; } = 20000;
    public double FloorDb { get; set; } = -70;
    /// <summary>Faint vertical frequency lines behind the curve.</summary>
    public bool GridLines { get; set; } = true;
}

public sealed class StatConfig
{
    public string Label { get; set; } = "";
    public string Address { get; set; } = "";
    public string Unit { get; set; } = "";
    /// <summary>.NET format string for the value, e.g. "{0:0}" or "{0:0.0}".</summary>
    public string Format { get; set; } = "{0:0}";
    /// <summary>Show OnText/OffText (value ≥ 0.5) instead of the number.</summary>
    public bool OnOff { get; set; }
    public string OnText { get; set; } = "ON";
    public string OffText { get; set; } = "OFF";
    /// <summary>No message for this long → show OffText (or "–"). 0 = never time out.</summary>
    public int TimeoutMs { get; set; }
}

public sealed class ButtonsConfig
{
    public int Columns { get; set; } = 4;
    public List<ActionButtonConfig> Items { get; set; } = new();
}

public sealed class ActionButtonConfig
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>toggle | momentary | trigger</summary>
    public string Kind { get; set; } = "toggle";
    /// <summary>[0] = released/off, [1] = on. Feedback ≥ 0.5 selects [1].</summary>
    public List<ButtonStateConfig> States { get; set; } = new();
    public string OscOut { get; set; } = "";
    public string OscFeedback { get; set; } = "";
    public float ValueOn { get; set; } = 1f;
    public float ValueOff { get; set; }
}

public sealed class ButtonStateConfig
{
    /// <summary>Visual style: idle | playing | active | off.</summary>
    public string Name { get; set; } = "idle";
    public string Label { get; set; } = "";
}

public sealed class OverlayConfig
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string OscFeedback { get; set; } = "";
    public string Format { get; set; } = "{0:0}";
}

public sealed class ActivityConfig
{
    public const string OscOut = "@osc-out";

    public string Label { get; set; } = "";
    /// <summary>OSC address that flashes the dot, or "@osc-out" for the kiosk's own sends.</summary>
    public string Address { get; set; } = "";
}

public sealed class InputMetersConfig
{
    public bool Enabled { get; set; } = true;
    public int Count { get; set; } = 16;
    /// <summary>{0} is replaced by the channel number (1-based).</summary>
    public string AddressPattern { get; set; } = "/vibesbox/in/ch/{0}";
    /// <summary>Levels (0..1) below this count as silence.</summary>
    public double Threshold { get; set; } = 0.15;
    public List<MeterGroupConfig> Groups { get; set; } = new();
}

public sealed class MeterGroupConfig
{
    public string Label { get; set; } = "";
    /// <summary>First channel of the group (1-based).</summary>
    public int Start { get; set; } = 1;
    public int Count { get; set; } = 1;
}

public sealed class NowPlayingConfig
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "";
    public double AutoHideSeconds { get; set; } = 10;
    public double ReshowSeconds { get; set; } = 10;
    /// <summary>Removed from the front of source names, e.g. "Bluetooth: ".</summary>
    public string StripSourcePrefix { get; set; } = "";
    /// <summary>Source-name renames, e.g. { "Lyrion": "Squeezebox" }.</summary>
    public Dictionary<string, string> SourceAliases { get; set; } = new();
    /// <summary>Producers drawn in the secondary colour instead of the accent.</summary>
    public List<string> SecondaryColorProducers { get; set; } = new();
}

/// <summary>
/// On the rising edge of a boolean field in a WebSocket JSON message, send an
/// OSC value — unless the feedback address shows it is already set.
/// </summary>
public sealed class AutomationConfig
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string Url { get; set; } = "";
    /// <summary>Top-level boolean JSON field to watch.</summary>
    public string Field { get; set; } = "";
    public string Address { get; set; } = "";
    public float Value { get; set; } = 1f;
    /// <summary>Defaults to Address when empty.</summary>
    public string FeedbackAddress { get; set; } = "";
    /// <summary>Send even before any feedback has been seen.</summary>
    public bool SendWhenUnknown { get; set; }
}

public sealed class PowerConfig
{
    public bool Shutdown { get; set; } = true;
    public bool Restart { get; set; } = true;
    public bool DisplayOff { get; set; } = true;
    /// <summary>Sent before shutdown/restart, e.g. to save and quit the DAW.</summary>
    public string BeforeShutdownAddress { get; set; } = "";
}

// AOT-safe JSON source generation; camelCase matches the file.
[JsonSerializable(typeof(KioskConfig))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
internal partial class KioskConfigJsonContext : JsonSerializerContext { }
