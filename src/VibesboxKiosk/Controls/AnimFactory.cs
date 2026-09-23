using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace VibesboxKiosk.Controls;

/// <summary>
/// Shared storyboard-animation factory for the entrance/exit motion used by
/// NowPlayingCard and OverlayHud (each previously carried an identical private
/// copy). Only the wire-up (target/property/From handling) is shared — each
/// control keeps its own timing/scale constants.
/// Import with <c>using static VibesboxKiosk.Controls.AnimFactory;</c> so call
/// sites read as a plain <c>Anim(...)</c>.
/// </summary>
internal static class AnimFactory
{
    public static DoubleAnimation Anim(
        DependencyObject target, string property, double? from, double to, double ms, EasingFunctionBase ease)
    {
        var a = new DoubleAnimation { To = to, Duration = TimeSpan.FromMilliseconds(ms), EasingFunction = ease };
        if (from is double f) a.From = f;
        Storyboard.SetTarget(a, target);
        Storyboard.SetTargetProperty(a, property);
        return a;
    }
}
