using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using VibesboxKiosk.Services;
using Windows.UI;

namespace VibesboxKiosk.Controls;

public sealed partial class IndicatorDot : UserControl
{
    // Off-state fill: the background lifted slightly towards the muted colour.
    private readonly Color OffColor = ThemeService.DotOff;

    public static readonly DependencyProperty DotSizeProperty =
        DependencyProperty.Register(nameof(DotSize), typeof(double), typeof(IndicatorDot),
            new PropertyMetadata(10.0, (d, e) => ((IndicatorDot)d).ApplySize((double)e.NewValue)));

    public static readonly DependencyProperty DotColorProperty =
        DependencyProperty.Register(nameof(DotColor), typeof(Color), typeof(IndicatorDot),
            new PropertyMetadata(Color.FromArgb(255, 0xFF, 0x70, 0x49), // replaced by ThemeService.Accent in the ctor
                (d, _) => ((IndicatorDot)d).ApplyIntensity(((IndicatorDot)d).Intensity)));

    public static readonly DependencyProperty IntensityProperty =
        DependencyProperty.Register(nameof(Intensity), typeof(double), typeof(IndicatorDot),
            new PropertyMetadata(0.0, (d, e) => ((IndicatorDot)d).ApplyIntensity((double)e.NewValue)));

    // Convenience bool: maps to Intensity 0 or 1
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(IndicatorDot),
            new PropertyMetadata(false, (d, e) => ((IndicatorDot)d).Intensity = (bool)e.NewValue ? 1.0 : 0.0));

    public double DotSize
    {
        get => (double)GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    public Color DotColor
    {
        get => (Color)GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    public double Intensity
    {
        get => (double)GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    private DropShadow? _glowShadow;

    public IndicatorDot()
    {
        InitializeComponent();
        DotColor = ThemeService.Accent;
        ApplySize(DotSize);
        Loaded += (_, _) =>
        {
            SetupGlow();
            ApplyIntensity(Intensity);
        };
    }

    private void ApplySize(double size)
    {
        Width = size;
        Height = size;
        Dot.Width = size;
        Dot.Height = size;
    }

    private void SetupGlow()
    {
        var hostVisual = ElementCompositionPreview.GetElementVisual(Dot);
        var compositor = hostVisual.Compositor;

        _glowShadow = compositor.CreateDropShadow();
        _glowShadow.BlurRadius = (float)(DotSize * 1.2f);
        _glowShadow.Offset = Vector3.Zero;
        _glowShadow.Color = Color.FromArgb(0, DotColor.R, DotColor.G, DotColor.B);

        var sv = compositor.CreateSpriteVisual();
        sv.Brush = compositor.CreateColorBrush(Color.FromArgb(1, DotColor.R, DotColor.G, DotColor.B));
        sv.Shadow = _glowShadow;

        var expr = compositor.CreateExpressionAnimation("host.Size");
        expr.SetReferenceParameter("host", hostVisual);
        sv.StartAnimation("Size", expr);

        ElementCompositionPreview.SetElementChildVisual(Dot, sv);
    }

    private void ApplyIntensity(double intensity)
    {
        var c = DotColor;

        if (intensity <= 0)
        {
            Dot.Fill = new SolidColorBrush(OffColor);
        }
        else
        {
            double t = Math.Clamp(intensity, 0, 1);
            byte r = (byte)(OffColor.R + (c.R - OffColor.R) * t);
            byte g = (byte)(OffColor.G + (c.G - OffColor.G) * t);
            byte b = (byte)(OffColor.B + (c.B - OffColor.B) * t);
            Dot.Fill = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }

        if (_glowShadow != null)
        {
            // Glow ramps in above 30% intensity
            double glowT = Math.Max(0, (intensity - 0.3) / 0.7);
            byte glowA = (byte)(glowT * 200);
            _glowShadow.Color = Color.FromArgb(glowA, DotColor.R, DotColor.G, DotColor.B);
        }
    }
}
