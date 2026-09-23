using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using VibesboxKiosk.Services;
using Windows.UI;

namespace VibesboxKiosk.Controls;

public sealed partial class IndicatorDotRow : UserControl
{
    public static readonly DependencyProperty GroupLabelProperty =
        DependencyProperty.Register(nameof(GroupLabel), typeof(string), typeof(IndicatorDotRow),
            new PropertyMetadata(string.Empty, (d, e) => ((IndicatorDotRow)d).GroupLabelText.Text = (string)e.NewValue));

    public static readonly DependencyProperty DotCountProperty =
        DependencyProperty.Register(nameof(DotCount), typeof(int), typeof(IndicatorDotRow),
            new PropertyMetadata(1, (d, _) => ((IndicatorDotRow)d).RebuildDots()));

    public static readonly DependencyProperty ActiveMaskProperty =
        DependencyProperty.Register(nameof(ActiveMask), typeof(int), typeof(IndicatorDotRow),
            new PropertyMetadata(0, (d, _) => ((IndicatorDotRow)d).UpdateDotStates()));

    public static readonly DependencyProperty DotColorProperty =
        DependencyProperty.Register(nameof(DotColor), typeof(Color), typeof(IndicatorDotRow),
            new PropertyMetadata(Color.FromArgb(255, 0xFF, 0x70, 0x49),
                (d, _) => ((IndicatorDotRow)d).RebuildDots()));

    public static readonly DependencyProperty DotSizeProperty =
        DependencyProperty.Register(nameof(DotSize), typeof(double), typeof(IndicatorDotRow),
            new PropertyMetadata(10.0, (d, _) => ((IndicatorDotRow)d).RebuildDots()));

    public string GroupLabel
    {
        get => (string)GetValue(GroupLabelProperty);
        set => SetValue(GroupLabelProperty, value);
    }

    public int DotCount
    {
        get => (int)GetValue(DotCountProperty);
        set => SetValue(DotCountProperty, value);
    }

    // Bitmask: bit 0 = dot 0 active, bit 1 = dot 1 active, etc.
    public int ActiveMask
    {
        get => (int)GetValue(ActiveMaskProperty);
        set => SetValue(ActiveMaskProperty, value);
    }

    public Color DotColor
    {
        get => (Color)GetValue(DotColorProperty);
        set => SetValue(DotColorProperty, value);
    }

    public double DotSize
    {
        get => (double)GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    public IndicatorDotRow()
    {
        InitializeComponent();
        DotColor = ThemeService.Secondary;
        Loaded += (_, _) => RebuildDots();
    }

    private void RebuildDots()
    {
        if (DotsPanel is null) return;
        DotsPanel.Children.Clear();
        for (int i = 0; i < DotCount; i++)
        {
            DotsPanel.Children.Add(new IndicatorDot
            {
                DotColor  = DotColor,
                DotSize   = DotSize,
                IsActive  = ((ActiveMask >> i) & 1) == 1,
            });
        }
    }

    private void UpdateDotStates()
    {
        if (DotsPanel is null) return;
        for (int i = 0; i < DotsPanel.Children.Count; i++)
        {
            if (DotsPanel.Children[i] is IndicatorDot dot)
                dot.IsActive = ((ActiveMask >> i) & 1) == 1;
        }
    }
}
