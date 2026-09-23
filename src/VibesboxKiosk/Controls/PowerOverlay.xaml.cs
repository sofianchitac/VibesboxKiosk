using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace VibesboxKiosk.Controls;

public sealed partial class PowerOverlay : UserControl
{
    public event EventHandler? ShutdownClicked;
    public event EventHandler? RestartClicked;
    public event EventHandler? DisplayOffClicked;
    public event EventHandler? DismissClicked;

    public PowerOverlay() => InitializeComponent();

    public void Configure(bool shutdown, bool restart, bool displayOff)
    {
        ShutdownItem.Visibility   = shutdown   ? Visibility.Visible : Visibility.Collapsed;
        RestartItem.Visibility    = restart    ? Visibility.Visible : Visibility.Collapsed;
        DisplayOffItem.Visibility = displayOff ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Show()
    {
        IsHitTestVisible = true;
        FadeTo(1.0, 200);
    }

    public void Hide()
    {
        IsHitTestVisible = false;
        FadeTo(0.0, 200);
    }

    private void Shutdown_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        ShutdownClicked?.Invoke(this, EventArgs.Empty);
    }

    private void Restart_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        RestartClicked?.Invoke(this, EventArgs.Empty);
    }

    private void DisplayOff_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        Hide();
        DisplayOffClicked?.Invoke(this, EventArgs.Empty);
    }

    private void Backdrop_Tapped(object sender, TappedRoutedEventArgs e)
    {
        DismissClicked?.Invoke(this, EventArgs.Empty);
        Hide();
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
