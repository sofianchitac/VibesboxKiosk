using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VibesboxKiosk.Config;
using VibesboxKiosk.Services;
using Windows.Foundation;

namespace VibesboxKiosk.Controls;

// One dot per input channel; brightness follows the channel's level (0.0–1.0).
// Optional group labels (e.g. "NDI", "S/PDIF") sit centred under their dot range.
// Dots shrink and tighten so any channel count fits within MaxBarWidth.
public sealed partial class InputDotBar : UserControl
{
    private const double MaxDotDiameter = 16.0;
    private const double MaxDotSpacing  = 34.0; // center-to-center (16px dot + 18px gap)
    private const double MaxBarWidth    = 560.0;
    private const double LabelGap       = 16.0;
    private const double LabelHeight    = 18.0;

    private int _count;
    private IReadOnlyList<MeterGroupConfig> _groups = Array.Empty<MeterGroupConfig>();
    private IndicatorDot[] _dots = Array.Empty<IndicatorDot>();
    private double _dotDiameter, _dotSpacing;

    public InputDotBar()
    {
        InitializeComponent();
    }

    /// <summary>Builds the dots and group labels. Call once, before the control loads.</summary>
    public void Configure(int count, IReadOnlyList<MeterGroupConfig> groups)
    {
        _count       = count;
        _groups      = groups;

        _dotSpacing  = count > 1 ? Math.Min(MaxDotSpacing, (MaxBarWidth - MaxDotDiameter) / (count - 1)) : MaxDotSpacing;
        _dotDiameter = Math.Min(MaxDotDiameter, Math.Max(4, _dotSpacing * 0.6));

        double totalWidth  = count > 0 ? (count - 1) * _dotSpacing + _dotDiameter : 0;
        double totalHeight = count > 0 ? _dotDiameter + LabelGap + LabelHeight : 0;
        RootCanvas.Width  = Width  = totalWidth;
        RootCanvas.Height = Height = totalHeight;

        RootCanvas.Children.Clear();
        BuildDots();
        BuildLabels();
    }

    // Called by IndicatorService at 30 Hz.
    public void SetIntensities(float[] intensities)
    {
        int len = Math.Min(intensities.Length, _dots.Length);
        for (int i = 0; i < len; i++)
            _dots[i].Intensity = intensities[i];
    }

    private void BuildDots()
    {
        _dots = new IndicatorDot[_count];
        for (int i = 0; i < _count; i++)
        {
            var dot = new IndicatorDot
            {
                DotSize   = _dotDiameter,
                DotColor  = ThemeService.Accent,
                Intensity = 0,
            };
            Canvas.SetLeft(dot, i * _dotSpacing);
            Canvas.SetTop(dot, 0);
            RootCanvas.Children.Add(dot);
            _dots[i] = dot;
        }
    }

    private void BuildLabels()
    {
        var font  = Application.Current.Resources["PrimaryFontFamilyMedium"] as FontFamily;
        var brush = Application.Current.Resources["TextMutedBrush"] as Brush;

        foreach (var g in _groups)
        {
            int start = Math.Clamp(g.Start - 1, 0, Math.Max(0, _count - 1));
            int end   = Math.Clamp(start + g.Count - 1, start, Math.Max(0, _count - 1));
            if (_count == 0 || string.IsNullOrEmpty(g.Label)) continue;

            double groupCenter = (start * _dotSpacing + end * _dotSpacing + _dotDiameter) / 2.0;

            var tb = new TextBlock
            {
                Text             = g.Label,
                FontSize         = 12,
                CharacterSpacing = 150,
                IsHitTestVisible = false,
            };
            if (font is not null) tb.FontFamily = font;
            if (brush is not null) tb.Foreground = brush;

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, groupCenter - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, _dotDiameter + LabelGap);
            RootCanvas.Children.Add(tb);
        }
    }
}
