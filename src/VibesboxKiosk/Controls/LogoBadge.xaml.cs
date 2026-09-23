using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using VibesboxKiosk.Config;
using VibesboxKiosk.Services;
using Windows.Storage.Streams;

namespace VibesboxKiosk.Controls;

/// <summary>
/// Header logo with two images: the normal one and the one shown while the logo
/// toggle is on (cross-faded). Either can be a user file (SVG, PNG, JPG); empty
/// paths fall back to the built-in logos.
/// </summary>
public sealed partial class LogoBadge : UserControl
{
    private const string BuiltInLogo       = "ms-appx:///Assets/Logos/VibesboxDSP-logo-grey.svg";
    private const string BuiltInLogoActive = "ms-appx:///Assets/Logos/VibesboxDSP-logo-color.svg";

    // SVGs are rasterised at this multiple of the logo height: the dashboard's
    // virtual canvas can be scaled up 2× or more on large screens, times DPI.
    private const int RasterMultiple = 6;

    private bool _isColor;

    public event EventHandler<bool>? Toggled;

    public LogoBadge()
    {
        InitializeComponent();
    }

    public async void Configure(ThemeConfig theme)
    {
        Height = theme.LogoHeight;
        int rasterH = (int)Math.Ceiling(theme.LogoHeight * RasterMultiple);

        string normal = theme.Logo;
        string active = theme.LogoActive.Length > 0 ? theme.LogoActive : theme.Logo;

        GreyLogo.Source  = await LoadAsync(normal, BuiltInLogo, rasterH);
        ColorLogo.Source = await LoadAsync(active, active.Length > 0 ? BuiltInLogo : BuiltInLogoActive, rasterH);
    }

    private static async Task<ImageSource?> LoadAsync(string path, string builtIn, int rasterH)
    {
        bool isSvg = (path.Length > 0 ? path : builtIn).EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

        if (path.Length == 0)
        {
            if (!isSvg) return new BitmapImage(new Uri(builtIn));
            return new SvgImageSource(new Uri(builtIn)) { RasterizePixelHeight = rasterH };
        }

        try
        {
            // Load through a stream: unpackaged apps can't rely on file:// URIs.
            var bytes  = await File.ReadAllBytesAsync(path);
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            if (isSvg)
            {
                var svg = new SvgImageSource { RasterizePixelHeight = rasterH };
                await svg.SetSourceAsync(stream);
                return svg;
            }

            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream);
            return bmp;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Logo] Can't load '{path}': {ex.Message} — using the built-in logo.");
            return await LoadAsync("", builtIn, rasterH);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        _isColor = !_isColor;
        CrossFade(_isColor);
        Toggled?.Invoke(this, _isColor);
    }

    // Programmatic toggle (OSC feedback).
    public void SetColor(bool color)
    {
        if (color == _isColor) return;
        _isColor = color;
        CrossFade(color);
    }

    private void CrossFade(bool toColor)
    {
        FadeTo(ColorLogo, toColor ? 1.0 : 0.0);
        FadeTo(GreyLogo,  toColor ? 0.0 : 1.0);
    }

    private static void FadeTo(UIElement target, double to)
    {
        var anim = new DoubleAnimation
        {
            To             = to,
            Duration       = TimeSpan.FromMilliseconds(300),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        var sb = new Storyboard();
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        sb.Begin();
    }
}
