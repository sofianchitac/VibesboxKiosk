using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using static VibesboxKiosk.Controls.AnimFactory;

namespace VibesboxKiosk.Controls;

public sealed partial class OverlayHud : UserControl
{
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _visible;

    public OverlayHud()
    {
        InitializeComponent();
        _hideTimer.Tick += (_, _) => Hide();
    }

    public void Show(string label, string value)
    {
        LabelText.Text = label;
        ValueText.Text = value;

        _hideTimer.Stop();
        _hideTimer.Start();

        if (!_visible)
        {
            _visible = true;
            IsHitTestVisible = true;
            AnimateIn();
        }
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _visible = false;
        IsHitTestVisible = false;
        AnimateOut();
    }

    // Entrance: fade in while the value pops up + rises with a gentle overshoot.
    private void AnimateIn()
    {
        var settle = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut };
        var sb = new Storyboard();
        sb.Children.Add(Anim(this, "Opacity", null, 1.0, 200, new CubicEase { EasingMode = EasingMode.EaseOut }));
        sb.Children.Add(Anim(ContentTransform, "ScaleX", 0.92, 1.0, 340, settle));
        sb.Children.Add(Anim(ContentTransform, "ScaleY", 0.92, 1.0, 340, settle));
        sb.Children.Add(Anim(ContentTransform, "TranslateY", 12, 0, 340, settle));
        sb.Begin();
    }

    // Exit: fade out while the value recedes slightly back into space.
    private void AnimateOut()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var sb = new Storyboard();
        sb.Children.Add(Anim(this, "Opacity", null, 0.0, 280, ease));
        sb.Children.Add(Anim(ContentTransform, "ScaleX", null, 0.96, 280, ease));
        sb.Children.Add(Anim(ContentTransform, "ScaleY", null, 0.96, 280, ease));
        sb.Children.Add(Anim(ContentTransform, "TranslateY", null, 8, 280, ease));
        sb.Begin();
    }
}
