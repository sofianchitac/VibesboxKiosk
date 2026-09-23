using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace VibesboxKiosk.Controls;

public sealed partial class ShutdownOverlay : UserControl
{
    public ShutdownOverlay() => InitializeComponent();

    public void Show(string label)
    {
        LabelText.Text = label;
        IsHitTestVisible = true;
        FadeTo(1.0, 200);
    }

    private void FadeTo(double target, int durationMs)
    {
        var anim = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var sb = new Storyboard();
        Storyboard.SetTarget(anim, this);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        sb.Begin();
    }
}
