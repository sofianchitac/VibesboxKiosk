using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VibesboxKiosk.Controls;

public sealed partial class StatPair : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatPair),
            new PropertyMetadata(string.Empty, (d, e) => ((StatPair)d).LabelText.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatPair),
            new PropertyMetadata(string.Empty, (d, e) => ((StatPair)d).ValueText.Text = (string)e.NewValue));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(StatPair),
            new PropertyMetadata(string.Empty, (d, e) => ((StatPair)d).UnitText.Text = (string)e.NewValue));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public StatPair() => InitializeComponent();
}
