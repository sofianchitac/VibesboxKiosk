using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using VibesboxKiosk.Services;
using Windows.UI;

namespace VibesboxKiosk.Controls;

public enum ActionButtonState { Idle, Playing, Active, Off }

public sealed partial class ActionButton : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ActionButton),
            new PropertyMetadata(string.Empty, (d, e) =>
            {
                var b = (ActionButton)d;
                b.TitleText.Text = (string)e.NewValue;
                // Mirror the visible label into the UIA Name so screen readers and
                // `winapp ui` see the live button name (set from config at runtime).
                AutomationProperties.SetName(b, (string)e.NewValue);
            }));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(ActionButton),
            new PropertyMetadata(string.Empty, (d, e) => ((ActionButton)d).StatusText.Text = (string)e.NewValue));

    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(ActionButtonState), typeof(ActionButton),
            new PropertyMetadata(ActionButtonState.Idle, (d, e) => ((ActionButton)d).ApplyState((ActionButtonState)e.NewValue)));

    public static readonly DependencyProperty ButtonIdProperty =
        DependencyProperty.Register(nameof(ButtonId), typeof(string), typeof(ActionButton),
            new PropertyMetadata(string.Empty, (d, e) =>
                // Expose the config id as the stable UIA AutomationId
                // so UI tests can target each button by a fixed selector.
                AutomationProperties.SetAutomationId((ActionButton)d, (string)e.NewValue)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ActionButtonState State
    {
        get => (ActionButtonState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string ButtonId
    {
        get => (string)GetValue(ButtonIdProperty);
        set => SetValue(ButtonIdProperty, value);
    }

    public event RoutedEventHandler? ButtonTapped;
    public event RoutedEventHandler? ButtonPressed;
    public event RoutedEventHandler? ButtonReleased;

    private DropShadow? _glowShadow;
    private Visual?     _rootVisual;
    private Compositor? _compositor;

    // Resting depth halo per state: Off sits flush (recessed), Idle/Playing get a
    // faint neutral lift (suspended), Active is energised forward with the accent glow.
    private static Color GlowAccent  => ThemeService.WithAlpha(ThemeService.Accent, 190);
    private static Color GlowNeutral => ThemeService.WithAlpha(ThemeService.Text, 0x26);
    private static Color GlowNone    => ThemeService.WithAlpha(ThemeService.Accent, 0);

    private const float GlowBlurRest  = 22f;
    private const float GlowBlurPress = 11f;

    public ActionButton()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            SetupGlow();
            SetupInteraction();
            ApplyState(State);
        };
        Tapped += (s, e) => ButtonTapped?.Invoke(s, e);
        PointerPressed  += (s, e) => { AnimatePress();   ButtonPressed?.Invoke(s, e);  };
        PointerReleased += (s, e) => { AnimateRelease(); ButtonReleased?.Invoke(s, e); };
        // Restore the resting pose if the touch slides off or is cancelled mid-press.
        PointerExited      += (_, _) => AnimateRelease();
        PointerCanceled    += (_, _) => AnimateRelease();
        PointerCaptureLost += (_, _) => AnimateRelease();
    }

    private void SetupGlow()
    {
        var hostVisual = ElementCompositionPreview.GetElementVisual(GlowHost);
        var compositor = hostVisual.Compositor;

        _glowShadow = compositor.CreateDropShadow();
        _glowShadow.BlurRadius = 22.0f;
        _glowShadow.Color = GlowNone;
        _glowShadow.Offset = Vector3.Zero;

        var sv = compositor.CreateSpriteVisual();
        sv.Brush = compositor.CreateColorBrush(ThemeService.WithAlpha(ThemeService.Accent, 1));
        sv.Shadow = _glowShadow;

        var sizeExpr = compositor.CreateExpressionAnimation("host.Size");
        sizeExpr.SetReferenceParameter("host", hostVisual);
        sv.StartAnimation("Size", sizeExpr);

        ElementCompositionPreview.SetElementChildVisual(GlowHost, sv);
    }

    private void SetupInteraction()
    {
        _rootVisual = ElementCompositionPreview.GetElementVisual(RootBorder);
        _compositor = _rootVisual.Compositor;

        // Pivot Scale around the button's centre, tracking layout size changes.
        var centerBind = _compositor.CreateExpressionAnimation(
            "Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
        _rootVisual.StartAnimation("CenterPoint", centerBind);
    }

    // Press: a quick, soft squish — dips and squashes a touch more vertically.
    private void AnimatePress()
    {
        if (_rootVisual is null || _compositor is null) return;

        var squish = _compositor.CreateSpringVector3Animation();
        squish.FinalValue   = new Vector3(0.965f, 0.925f, 1f);
        squish.DampingRatio = 0.78f;
        squish.Period       = TimeSpan.FromMilliseconds(45);
        _rootVisual.StartAnimation("Scale", squish);

        AnimateGlowBlur(GlowBlurPress, dampingRatio: 0.8f);
    }

    // Release: spring back to rest with one gentle overshoot, halo blooms back.
    private void AnimateRelease()
    {
        if (_rootVisual is null || _compositor is null) return;

        var bounce = _compositor.CreateSpringVector3Animation();
        bounce.FinalValue   = Vector3.One;
        bounce.DampingRatio = 0.5f;
        bounce.Period       = TimeSpan.FromMilliseconds(60);
        _rootVisual.StartAnimation("Scale", bounce);

        AnimateGlowBlur(GlowBlurRest, dampingRatio: 0.5f);
    }

    private void AnimateGlowBlur(float target, float dampingRatio)
    {
        if (_glowShadow is null || _compositor is null) return;

        var anim = _compositor.CreateSpringScalarAnimation();
        anim.FinalValue   = target;
        anim.DampingRatio = dampingRatio;
        anim.Period       = TimeSpan.FromMilliseconds(55);
        _glowShadow.StartAnimation("BlurRadius", anim);
    }

    private void ApplyState(ActionButtonState state)
    {
        if (_glowShadow != null)
            _glowShadow.Color = state switch
            {
                ActionButtonState.Active => GlowAccent,
                ActionButtonState.Off    => GlowNone,
                _                        => GlowNeutral, // Idle / Playing: gentle lift
            };

        var vsName = state switch
        {
            ActionButtonState.Playing => "StatePlaying",
            ActionButtonState.Active  => "StateActive",
            ActionButtonState.Off     => "StateOff",
            _                         => "StateIdle",
        };
        VisualStateManager.GoToState(this, vsName, true);
    }
}
