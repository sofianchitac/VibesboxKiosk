using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VibesboxKiosk.Config;
using Windows.UI;

namespace VibesboxKiosk.Services;

/// <summary>
/// Applies the user's colours. Recolours the shared brushes in Themes/Tokens.xaml
/// in place (every {StaticResource} user updates) and exposes the raw colours for
/// code that builds brushes, shadows and animations itself. Controls read these
/// when they are built; the dashboard is rebuilt on every config change.
/// </summary>
public static class ThemeService
{
    public static Color Background { get; private set; }
    public static Color Surface    { get; private set; }
    public static Color Text       { get; private set; }
    public static Color Muted      { get; private set; }
    public static Color Accent     { get; private set; }
    public static Color AccentSoft { get; private set; }
    public static Color Secondary  { get; private set; }

    /// <summary>Unlit dot: the background lifted slightly towards Muted.</summary>
    public static Color DotOff => Mix(Background, Muted, 0.2);

    public static void Apply(ThemeConfig t)
    {
        var d = new ThemeConfig();
        Background = Parse(t.Background, d.Background);
        Surface    = Parse(t.Surface,    d.Surface);
        Text       = Parse(t.Text,       d.Text);
        Muted      = Parse(t.Muted,      d.Muted);
        Accent     = Parse(t.Accent,     d.Accent);
        AccentSoft = Parse(t.AccentSoft, d.AccentSoft);
        Secondary  = Parse(t.Secondary,  d.Secondary);

        Set("BgPrimaryBrush", Background);
        Set("BgSecondaryBrush", Surface);
        Set("BtnOffBrush", Surface);
        Set("TextPrimaryBrush", Text);
        Set("BtnActiveBrush", Text);
        Set("TextMutedBrush", Muted);
        Set("BtnIdleBrush", Muted);
        Set("ColorOrangeBrush", Accent);
        Set("BtnPlayingBrush", Accent);
        Set("ColorOrangeLightBrush", AccentSoft);
        Set("SpectrumLineBrush", AccentSoft);
        Set("ColorCyanBrush", Secondary);

        if (Application.Current.Resources.TryGetValue("SpectrumFillBrush", out var o) &&
            o is LinearGradientBrush fill && fill.GradientStops.Count == 2)
        {
            fill.GradientStops[0].Color = WithAlpha(Muted, 0xFF);
            fill.GradientStops[1].Color = WithAlpha(Muted, 0x00);
        }
    }

    public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        255,
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    /// <summary>Parses #RGB, #RRGGBB or #AARRGGBB; falls back on anything else.</summary>
    public static Color Parse(string? hex, string fallback)
    {
        if (TryParse(hex, out var c)) return c;
        TryParse(fallback, out c);
        return c;
    }

    public static bool TryParse(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;
        color = Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static string ToHex(Color c) => c.A == 255
        ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
        : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static void Set(string key, Color color)
    {
        if (Application.Current.Resources.TryGetValue(key, out var o) && o is SolidColorBrush b)
            b.Color = color;
    }
}
