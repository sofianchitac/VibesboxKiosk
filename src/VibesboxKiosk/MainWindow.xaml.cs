using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using VibesboxKiosk.Config;
using VibesboxKiosk.Pages;
using VibesboxKiosk.Services;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace VibesboxKiosk;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Ctrl+, opens the settings (for keyboard / remote-desktop use; on the
        // touchscreen it's a press-and-hold on the logo).
        var openSettings = new KeyboardAccelerator { Key = (VirtualKey)188, Modifiers = VirtualKeyModifiers.Control };
        openSettings.Invoked += (_, e) => { e.Handled = true; ShowSettings(); };
        Host.KeyboardAccelerators.Add(openSettings);
        Host.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        ApplyDisplay(ConfigService.Instance.Current.Display);
        ShowDashboard();

        ConfigService.Instance.ConfigReloaded += (_, _) => DispatcherQueue.TryEnqueue(OnConfigChanged);
        Closed += (_, _) => KioskServices.StopAll();
    }

    // Hand edits to the file apply live; while the settings page is open it owns
    // the config and rebuilds the dashboard itself on close.
    private void OnConfigChanged()
    {
        if (Host.Child is SettingsPage) return;
        Rebuild();
    }

    private DisplayConfig? _appliedDisplay;

    private void Rebuild()
    {
        var cfg = ConfigService.Instance.Current;
        ThemeService.Apply(cfg.Theme);
        try { ApplyDisplay(cfg.Display); }
        catch (Exception ex) { Log.Error("[Window] Applying display settings failed", ex); }
        ShowDashboard();
    }

    private void ShowDashboard()
    {
        KioskServices.StopAll();
        var page = new DashboardPage();
        page.SettingsRequested += (_, _) => ShowSettings();
        Host.Child = page;
    }

    private void ShowSettings()
    {
        if (Host.Child is SettingsPage) return;
        KioskServices.StopAll();   // also frees the OSC listen port while it is being edited
        var page = new SettingsPage();
        page.Closed += (_, _) => Rebuild();
        page.QuitRequested += (_, _) => Close();
        Host.Child = page;
    }

    // ── Window placement ─────────────────────────────────────────────────────

    private void ApplyDisplay(DisplayConfig d)
    {
        // Re-placing the window leaves and re-enters full screen (a visible
        // flash), so only do it when the display settings actually changed.
        if (_appliedDisplay is { } a && a.Fullscreen == d.Fullscreen && a.AlwaysOnTop == d.AlwaysOnTop && a.Monitor == d.Monitor)
            return;
        _appliedDisplay = new DisplayConfig { Fullscreen = d.Fullscreen, AlwaysOnTop = d.AlwaysOnTop, Monitor = d.Monitor };

        var hwnd = WindowNative.GetWindowHandle(this);
        var area = PickDisplay(d.Monitor);

        // Leave full screen first so the move lands on the chosen display.
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        // Full screen hides the title bar by itself; windowed mode keeps the
        // standard one (setting title-bar options without an extended title
        // bar throws "not in the correct state").
        if (d.Fullscreen)
        {
            AppWindow.Move(new PointInt32(area.OuterBounds.X, area.OuterBounds.Y));
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
        else
        {
            var wa = area.WorkArea;
            AppWindow.MoveAndResize(new RectInt32(wa.X + wa.Width / 8, wa.Y + wa.Height / 8, wa.Width * 3 / 4, wa.Height * 3 / 4));
        }

        // Presenters don't manage Z-order across processes; pin (or unpin) the
        // window so other apps launched after the kiosk can't cover it.
        SetWindowPos(hwnd, d.AlwaysOnTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    }

    /// <summary>0 = primary; n = the n-th other display in enumeration order.</summary>
    internal static DisplayArea PickDisplay(int index)
    {
        var primary = DisplayArea.Primary;
        if (index <= 0) return primary;

        var all = DisplayArea.FindAll();
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var a = all[i];
            if (a.DisplayId.Value == primary.DisplayId.Value) continue;
            if (++n == index) return a;
        }
        return primary;
    }

    private static readonly IntPtr HWND_TOPMOST   = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
}
