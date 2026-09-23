using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using VibesboxKiosk.Config;
using VibesboxKiosk.Services;

namespace VibesboxKiosk.Pages;

/// <summary>
/// Settings editor. Works on a deep copy of the config; Save writes it (the
/// dashboard is then rebuilt from it), Cancel drops it. The form is built in code
/// from small field helpers, one section per navigation item.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public event EventHandler? Closed;
    public event EventHandler? QuitRequested;

    private readonly KioskConfig _cfg = ConfigService.Instance.CloneCurrent();

    private static readonly (string Tag, string Title, Symbol Icon)[] Sections =
    {
        ("display",     "Display",       Symbol.FullScreen),
        ("osc",         "Connection",    Symbol.Globe),
        ("appearance",  "Appearance",    Symbol.FontColor),
        ("header",      "Logo toggle",   Symbol.Favorite),
        ("spectrum",    "Spectrum",      Symbol.Volume),
        ("stats",       "Status row",    Symbol.List),
        ("buttons",     "Buttons",       Symbol.ViewAll),
        ("overlays",    "Value pop-ups", Symbol.Zoom),
        ("indicators",  "Indicators",    Symbol.Target),
        ("nowplaying",  "Now Playing",   Symbol.Audio),
        ("automations", "Automations",   Symbol.Repair),
        ("power",       "Power",         Symbol.Setting),
    };

    public SettingsPage()
    {
        InitializeComponent();
        PathText.Text = ConfigService.Instance.ConfigPath;

        foreach (var (tag, title, icon) in Sections)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Tag = tag };
            row.Children.Add(new SymbolIcon(icon));
            row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center });
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row, title);
            Nav.Items.Add(row);
        }
        // Select once loaded: selecting in the constructor renders the first
        // section before the page is in the tree, where theme lookups fail.
        Loaded += (_, _) => { if (Nav.SelectedIndex < 0) Nav.SelectedIndex = 0; };
    }

    private string _section = "display";

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is FrameworkElement { Tag: string tag })
        {
            _section = tag;
            Render();
        }
    }

    private void Render()
    {
        Form.Children.Clear();
        ContentScroll.ChangeView(null, 0, null, disableAnimation: true);
        switch (_section)
        {
            case "display":     BuildDisplay();     break;
            case "osc":         BuildOsc();         break;
            case "appearance":  BuildAppearance();  break;
            case "header":      BuildHeader();      break;
            case "spectrum":    BuildSpectrum();    break;
            case "stats":       BuildStats();       break;
            case "buttons":     BuildButtons();     break;
            case "overlays":    BuildOverlays();    break;
            case "indicators":  BuildIndicators();  break;
            case "nowplaying":  BuildNowPlaying();  break;
            case "automations": BuildAutomations(); break;
            case "power":       BuildPower();       break;
        }
    }

    // ── Sections ─────────────────────────────────────────────────────────────

    private void BuildDisplay()
    {
        var d = _cfg.Display;
        Title("Display", "Where and how the kiosk window appears. The layout scales to any resolution.");
        Add(Toggle("Full screen", d.Fullscreen, v => d.Fullscreen = v));
        Add(Toggle("Always on top", d.AlwaysOnTop, v => d.AlwaysOnTop = v, "Keeps other apps (e.g. the DAW) from covering the kiosk."));
        Add(Number("Display", d.Monitor, v => d.Monitor = (int)v, 0, 8, 1, "0 = primary display, 1.. = the others."));
        Add(Number("UI scale", d.UiScale, v => d.UiScale = v, 0.5, 3, 0.05, "Multiplier on the automatic fit-to-screen size."));
    }

    private void BuildOsc()
    {
        var o = _cfg.Osc;
        Title("Connection", "OSC over UDP to the DSP host (DAW, plugin host…).");
        Add(Text("Host", o.Host, v => o.Host = v, "127.0.0.1"));
        Add(Number("Send port", o.SendPort, v => o.SendPort = (int)v, 1, 65535, 1, "The host listens here."));
        Add(Number("Listen port", o.ListenPort, v => o.ListenPort = (int)v, 1, 65535, 1, "The kiosk receives feedback here."));
        Add(Text("Refresh address", o.RefreshAddress, v => o.RefreshAddress = v, "/vibesbox/refresh",
            "Sent once at start so the host can send every current value."));
    }

    private void BuildAppearance()
    {
        var t = _cfg.Theme;
        var d = new ThemeConfig();
        Title("Appearance", "Colours and logo. The theme is always dark.");
        Add(ColorField("Background", t.Background, d.Background, v => t.Background = v));
        Add(ColorField("Surface", t.Surface, d.Surface, v => t.Surface = v, "Buttons in the 'off' style."));
        Add(ColorField("Text", t.Text, d.Text, v => t.Text = v, "Also the 'active' button style."));
        Add(ColorField("Muted", t.Muted, d.Muted, v => t.Muted = v, "Labels, 'idle' buttons, spectrum fill."));
        Add(ColorField("Accent", t.Accent, d.Accent, v => t.Accent = v, "'Playing' buttons, meters, highlights."));
        Add(ColorField("Accent (soft)", t.AccentSoft, d.AccentSoft, v => t.AccentSoft = v, "Spectrum line and edge glow."));
        Add(ColorField("Secondary", t.Secondary, d.Secondary, v => t.Secondary = v, "Activity dots."));
        Add(FileField("Logo", t.Logo, v => t.Logo = v, "Empty = built-in logo."));
        Add(FileField("Logo (toggle on)", t.LogoActive, v => t.LogoActive = v, "Shown while the logo toggle is on."));
        Add(Number("Logo height", t.LogoHeight, v => t.LogoHeight = v, 8, 200, 1, "In layout units (the screen is at least 1024×768 units)."));
    }

    private void BuildHeader()
    {
        var h = _cfg.Header;
        Title("Logo toggle", "Tapping the logo toggles a value (e.g. a party or ambience mode). Press and hold the logo to open these settings.");
        Add(Text("OSC address", h.ToggleAddress, v => h.ToggleAddress = v, "/vibesbox/glow/enable", "Sends 1/0; feedback on the same address."));
        Add(Toggle("Edge glow", h.Glow, v => h.Glow = v, "The screen edges glow while the toggle is on."));
        Add(Text("Glow level address", h.GlowLevelAddress, v => h.GlowLevelAddress = v, "/vibesbox/master/level", "0..1 value driving the glow brightness."));
        Add(Text("WebSocket URL", h.WebSocketUrl, v => h.WebSocketUrl = v, "ws://host:port", "Optional: also send a message here."));
        Add(Text("Message when on", h.WebSocketOn, v => h.WebSocketOn = v));
        Add(Text("Message when off", h.WebSocketOff, v => h.WebSocketOff = v));
    }

    private void BuildSpectrum()
    {
        var s = _cfg.Spectrum;
        Title("Spectrum", "One OSC value per band, in dB.");
        Add(Toggle("Show spectrum", s.Enabled, v => s.Enabled = v));
        Add(Number("Bands", s.BandCount, v => s.BandCount = (int)v, 2, 512, 1));
        Add(Text("Address pattern", s.AddressPattern, v => s.AddressPattern = v, "/vibesbox/spectrum/{0}", "{0} = band number, starting at 0."));
        Add(Number("Lowest band (Hz)", s.MinHz, v => s.MinHz = v, 1, 1000, 1));
        Add(Number("Highest band (Hz)", s.MaxHz, v => s.MaxHz = v, 1000, 96000, 100));
        Add(Toggle("Grid lines", s.GridLines, v => s.GridLines = v, "Faint vertical frequency lines."));
        Add(Number("Floor (dB)", s.FloorDb, v => s.FloorDb = v, -200, -1, 1, "Values at or below this draw as silence."));
    }

    private void BuildStats()
    {
        Title("Status row", "Values shown under the spectrum.");
        ListEditor(_cfg.Stats, s => s.Label, () => new StatConfig { Label = "NEW" }, (s, p) =>
        {
            p.Children.Add(Text("Label", s.Label, v => s.Label = v));
            p.Children.Add(Text("OSC address", s.Address, v => s.Address = v));
            p.Children.Add(Text("Unit", s.Unit, v => s.Unit = v));
            p.Children.Add(Text("Format", s.Format, v => s.Format = v, "{0:0}", ".NET format, e.g. {0:0.0}"));
            p.Children.Add(Toggle("Show as on/off", s.OnOff, v => s.OnOff = v, "≥ 0.5 = on."));
            p.Children.Add(Text("On text", s.OnText, v => s.OnText = v));
            p.Children.Add(Text("Off text", s.OffText, v => s.OffText = v));
            p.Children.Add(Number("Timeout (ms)", s.TimeoutMs, v => s.TimeoutMs = (int)v, 0, 60000, 100,
                "No message for this long → off / –. 0 = never."));
        });
    }

    private static readonly string[] Kinds = { "toggle", "momentary", "trigger" };
    private static readonly string[] Styles = { "idle", "playing", "active", "off" };
    private static readonly string[] StyleNames = { "Idle (muted)", "Playing (accent)", "Active (bright)", "Off (dark)" };

    private void BuildButtons()
    {
        var b = _cfg.Buttons;
        Title("Buttons", "Each button sends OSC and follows its feedback address (≥ 0.5 = on).");
        Add(Number("Columns", b.Columns, v => b.Columns = (int)v, 1, 8, 1));
        ListEditor(b.Items, i => i.Name, () => new ActionButtonConfig
        {
            Name = "New",
            States = { new() { Name = "off", Label = "OFF" }, new() { Name = "playing", Label = "ON" } },
        }, (i, p) =>
        {
            p.Children.Add(Text("Name", i.Name, v => i.Name = v));
            p.Children.Add(Choice("Kind", Kinds, Kinds, i.Kind, v => i.Kind = v,
                "Toggle flips on tap; momentary is on while held; trigger sends 'on' once."));
            p.Children.Add(Text("OSC out", i.OscOut, v => i.OscOut = v));
            p.Children.Add(Text("OSC feedback", i.OscFeedback, v => i.OscFeedback = v));
            p.Children.Add(Number("Value on", i.ValueOn, v => i.ValueOn = (float)v, -100000, 100000, 1));
            p.Children.Add(Number("Value off", i.ValueOff, v => i.ValueOff = (float)v, -100000, 100000, 1));
            p.Children.Add(Text("Id", i.Id, v => i.Id = v, "", "Stable id, used as the UI-automation id."));
            for (int n = 0; n < i.States.Count && n < 2; n++)
            {
                var s = i.States[n];
                string which = n == 0 ? "Off" : "On";
                p.Children.Add(Choice($"{which} style", Styles, StyleNames, s.Name, v => s.Name = v));
                p.Children.Add(Text($"{which} status text", s.Label, v => s.Label = v));
            }
        });
    }

    private void BuildOverlays()
    {
        Title("Value pop-ups", "A large value pops up over the dashboard whenever its address changes (volume, EQ…).");
        ListEditor(_cfg.Overlays, o => o.Label, () => new OverlayConfig { Label = "NEW" }, (o, p) =>
        {
            p.Children.Add(Text("Label", o.Label, v => o.Label = v));
            p.Children.Add(Text("OSC address", o.OscFeedback, v => o.OscFeedback = v));
            p.Children.Add(Text("Format", o.Format, v => o.Format = v, "{0:0}", "e.g. {0:+0;-0;0} dB"));
        });
    }

    private void BuildIndicators()
    {
        Title("Activity dots", $"Flash when their address receives anything. Use {ActivityConfig.OscOut} for the kiosk's own outgoing OSC.");
        ListEditor(_cfg.Activity, a => a.Label, () => new ActivityConfig { Label = "NEW" }, (a, p) =>
        {
            p.Children.Add(Text("Label", a.Label, v => a.Label = v));
            p.Children.Add(Text("OSC address", a.Address, v => a.Address = v));
        });

        var m = _cfg.InputMeters;
        Title("Input meters", "One dot per channel; brightness follows its level (0..1).");
        Add(Toggle("Show input meters", m.Enabled, v => m.Enabled = v));
        Add(Number("Channels", m.Count, v => m.Count = (int)v, 0, 64, 1));
        Add(Text("Address pattern", m.AddressPattern, v => m.AddressPattern = v, "/vibesbox/in/{0}", "{0} = channel number, starting at 1."));
        Add(Number("Silence threshold", m.Threshold, v => m.Threshold = v, 0, 1, 0.01));
        Title("Channel groups", "Labels under a range of dots.");
        ListEditor(m.Groups, g => g.Label, () => new MeterGroupConfig { Label = "NEW" }, (g, p) =>
        {
            p.Children.Add(Text("Label", g.Label, v => g.Label = v));
            p.Children.Add(Number("First channel", g.Start, v => g.Start = (int)v, 1, 64, 1));
            p.Children.Add(Number("Channels", g.Count, v => g.Count = (int)v, 1, 64, 1));
        });
    }

    private void BuildNowPlaying()
    {
        var n = _cfg.NowPlaying;
        Title("Now Playing", "Track info from a WebSocket feed, shown over the spectrum. Tap the spectrum to show or hide it. Protocol: docs/now-playing.md.");
        Add(Toggle("Enabled", n.Enabled, v => n.Enabled = v));
        Add(Text("WebSocket URL", n.Url, v => n.Url = v, "ws://host:port/path"));
        Add(Number("Hide after (s)", n.AutoHideSeconds, v => n.AutoHideSeconds = v, 1, 600, 1));
        Add(Number("Show again after (s)", n.ReshowSeconds, v => n.ReshowSeconds = v, 1, 600, 1));
        Add(Text("Strip source prefix", n.StripSourcePrefix, v => n.StripSourcePrefix = v, "Bluetooth: "));
        Add(Text("Source renames", string.Join("; ", n.SourceAliases.Select(kv => $"{kv.Key}={kv.Value}")),
            v => n.SourceAliases = ParsePairs(v), "Lyrion=Squeezebox; …"));
        Add(Text("Secondary-colour producers", string.Join(", ", n.SecondaryColorProducers),
            v => n.SecondaryColorProducers = v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            "", "Comma-separated producer ids drawn in the secondary colour."));
    }

    private void BuildAutomations()
    {
        Title("Automations", "When a boolean field in a WebSocket JSON feed turns true, send an OSC value — skipped if the feedback shows it is already set.");
        ListEditor(_cfg.Automations, a => a.Name, () => new AutomationConfig { Name = "New rule" }, (a, p) =>
        {
            p.Children.Add(Text("Name", a.Name, v => a.Name = v));
            p.Children.Add(Toggle("Enabled", a.Enabled, v => a.Enabled = v));
            p.Children.Add(Text("WebSocket URL", a.Url, v => a.Url = v, "ws://host:port"));
            p.Children.Add(Text("JSON field", a.Field, v => a.Field = v, "multichannel"));
            p.Children.Add(Text("OSC address", a.Address, v => a.Address = v));
            p.Children.Add(Number("Value", a.Value, v => a.Value = (float)v, -100000, 100000, 1));
            p.Children.Add(Text("Feedback address", a.FeedbackAddress, v => a.FeedbackAddress = v, "", "Empty = same as the OSC address."));
            p.Children.Add(Toggle("Send when state unknown", a.SendWhenUnknown, v => a.SendWhenUnknown = v,
                "Send even before any feedback has arrived."));
        });
    }

    private void BuildPower()
    {
        var w = _cfg.Power;
        Title("Power", "Actions offered by the power button.");
        Add(Toggle("Shut down", w.Shutdown, v => w.Shutdown = v));
        Add(Toggle("Restart", w.Restart, v => w.Restart = v));
        Add(Toggle("Display off", w.DisplayOff, v => w.DisplayOff = v));
        Add(Text("Before shutdown, send", w.BeforeShutdownAddress, v => w.BeforeShutdownAddress = v, "/vibesbox/host/quit",
            "Sent 1.2 s before shutdown, e.g. to save and quit the DAW."));
    }

    // ── Field helpers ────────────────────────────────────────────────────────

    private void Add(UIElement e) => Form.Children.Add(e);

    // Styling lookups must never take the page down: a missing or unexpected
    // resource just leaves the default look.
    private static T? Res<T>(string key) where T : class =>
        Application.Current.Resources.TryGetValue(key, out var o) ? o as T : null;

    private static void Styled(TextBlock tb, string styleKey)
    {
        if (Res<Style>(styleKey) is { } style) tb.Style = style;
    }

    private static TextBlock Secondary(TextBlock tb)
    {
        if (Res<Brush>("TextFillColorSecondaryBrush") is { } b) tb.Foreground = b;
        else tb.Opacity = 0.7;
        return tb;
    }

    private void Title(string title, string description)
    {
        var head = new TextBlock { Text = title, Margin = new Thickness(0, 16, 0, 0) };
        Styled(head, "SubtitleTextBlockStyle");
        Form.Children.Add(head);
        Form.Children.Add(Secondary(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
        }));
    }

    // Label (+ optional help line) on the left, control on the right.
    private static FrameworkElement Row(string label, FrameworkElement control, string? help = null)
    {
        var grid = new Grid { ColumnSpacing = 16, MinHeight = 40 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap });
        if (help is not null)
        {
            var tb = new TextBlock { Text = help, TextWrapping = TextWrapping.Wrap };
            Styled(tb, "CaptionTextBlockStyle");
            left.Children.Add(Secondary(tb));
        }
        grid.Children.Add(left);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, label);
        return grid;
    }

    private static FrameworkElement Text(string label, string value, Action<string> set, string placeholder = "", string? help = null)
    {
        var tb = new TextBox { Text = value, PlaceholderText = placeholder, HorizontalAlignment = HorizontalAlignment.Stretch };
        tb.TextChanged += (_, _) => set(tb.Text);
        return Row(label, tb, help);
    }

    private static FrameworkElement Number(string label, double value, Action<double> set, double min, double max, double step, string? help = null)
    {
        var nb = new NumberBox
        {
            Value = value,
            Minimum = min,
            Maximum = max,
            SmallChange = step,
            LargeChange = step * 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 180,
        };
        nb.ValueChanged += (_, e) => { if (!double.IsNaN(e.NewValue)) set(e.NewValue); };
        return Row(label, nb, help);
    }

    private static FrameworkElement Toggle(string label, bool value, Action<bool> set, string? help = null)
    {
        var ts = new ToggleSwitch { IsOn = value };
        ts.Toggled += (_, _) => set(ts.IsOn);
        return Row(label, ts, help);
    }

    private static FrameworkElement Choice(string label, string[] values, string[] names, string current, Action<string> set, string? help = null)
    {
        var cb = new ComboBox { MinWidth = 220 };
        foreach (var n in names) cb.Items.Add(n);
        cb.SelectedIndex = Math.Max(0, Array.FindIndex(values, v => v.Equals(current, StringComparison.OrdinalIgnoreCase)));
        cb.SelectionChanged += (_, _) => { if (cb.SelectedIndex >= 0) set(values[cb.SelectedIndex]); };
        return Row(label, cb, help);
    }

    // Swatch button + hex box; the swatch opens a ColorPicker flyout.
    private static FrameworkElement ColorField(string label, string value, string fallback, Action<string> set, string? help = null)
    {
        var swatch = new Border { Width = 32, Height = 20, CornerRadius = new CornerRadius(3) };
        var hex    = new TextBox { Text = value, Width = 120 };
        var picker = new ColorPicker { IsAlphaEnabled = false, IsMoreButtonVisible = false, Color = ThemeService.Parse(value, fallback) };
        var button = new Button { Content = swatch, Flyout = new Flyout { Content = picker } };

        void Show(string h) => swatch.Background = new SolidColorBrush(ThemeService.Parse(h, fallback));
        Show(value);

        bool syncing = false;
        picker.ColorChanged += (_, e) =>
        {
            if (syncing) return;
            syncing = true;
            hex.Text = ThemeService.ToHex(e.NewColor);
            syncing = false;
        };
        hex.TextChanged += (_, _) =>
        {
            set(hex.Text);
            Show(hex.Text);
            if (!syncing && ThemeService.TryParse(hex.Text, out var c))
            {
                syncing = true;
                picker.Color = c;
                syncing = false;
            }
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(button);
        panel.Children.Add(hex);
        return Row(label, panel, help);
    }

    private FrameworkElement FileField(string label, string value, Action<string> set, string? help = null)
    {
        var tb = new TextBox { Text = value, PlaceholderText = "(built-in)" };
        tb.TextChanged += (_, _) => set(tb.Text);
        var browse = new Button { Content = "Browse…" };
        browse.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
            foreach (var ext in new[] { ".svg", ".png", ".jpg", ".jpeg" }) picker.FileTypeFilter.Add(ext);
            var result = await picker.PickSingleFileAsync();
            if (result is not null) tb.Text = result.Path;
        };

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(tb);
        Grid.SetColumn(browse, 1);
        grid.Children.Add(browse);
        return Row(label, grid, help);
    }

    // One expander per item with move / delete, plus an Add button.
    private void ListEditor<T>(List<T> items, Func<T, string> titleOf, Func<T> create, Action<T, StackPanel> buildForm)
    {
        var host = new StackPanel { Spacing = 4 };
        Form.Children.Add(host);

        void Rebuild(int expandIndex = -1)
        {
            host.Children.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                int index = i;
                var item = items[i];

                var body = new StackPanel { Spacing = 8 };
                buildForm(item, body);

                var header = new Grid();
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var title = new TextBlock { Text = Display(titleOf(item)), VerticalAlignment = VerticalAlignment.Center };
                header.Children.Add(title);

                var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                tools.Children.Add(ToolButton("", "Move up",   index > 0,               () => { Swap(items, index, index - 1); Rebuild(index - 1); }));
                tools.Children.Add(ToolButton("", "Move down", index < items.Count - 1, () => { Swap(items, index, index + 1); Rebuild(index + 1); }));
                tools.Children.Add(ToolButton("", "Delete",    true,                    () => { items.RemoveAt(index); Rebuild(); }));
                Grid.SetColumn(tools, 1);
                header.Children.Add(tools);

                // Keep the header title in step with the item's name while typing.
                foreach (var tb in FindTextBoxes(body))
                    tb.TextChanged += (_, _) => title.Text = Display(titleOf(item));

                host.Children.Add(new Expander
                {
                    Header = header,
                    Content = body,
                    IsExpanded = index == expandIndex,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                });
            }

            var add = new Button { Content = "Add", Margin = new Thickness(0, 4, 0, 0) };
            add.Click += (_, _) => { items.Add(create()); Rebuild(items.Count - 1); };
            host.Children.Add(add);
        }

        Rebuild();
    }

    private static string Display(string s) => string.IsNullOrWhiteSpace(s) ? "(unnamed)" : s;

    private static Button ToolButton(string glyph, string name, bool enabled, Action onClick)
    {
        var b = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            IsEnabled = enabled,
            Padding = new Thickness(8),
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, name);
        ToolTipService.SetToolTip(b, name);
        b.Click += (_, _) => onClick();
        return b;
    }

    private static IEnumerable<TextBox> FindTextBoxes(Panel panel)
    {
        foreach (var child in panel.Children)
        {
            if (child is TextBox tb) yield return tb;
            else if (child is Panel p)
                foreach (var inner in FindTextBoxes(p)) yield return inner;
        }
    }

    private static void Swap<T>(List<T> list, int a, int b) => (list[a], list[b]) = (list[b], list[a]);

    private static Dictionary<string, string> ParsePairs(string text)
    {
        var d = new Dictionary<string, string>();
        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0) d[part[..eq].Trim()] = part[(eq + 1)..].Trim();
        }
        return d;
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigService.Instance.Save(_cfg, raiseEvent: false);
        }
        catch (Exception ex)
        {
            Log.Error("[Settings] Save failed", ex);
            _ = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Couldn't save the settings",
                Content = ex.Message,
                CloseButtonText = "OK",
            }.ShowAsync();
            return;
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Closed?.Invoke(this, EventArgs.Empty);

    private async void Quit_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Quit the kiosk?",
            Content = "Unsaved changes are lost. The kiosk starts again at the next sign-in, or from the Start menu.",
            PrimaryButtonText = "Quit",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            QuitRequested?.Invoke(this, EventArgs.Empty);
    }
}
