using System.Diagnostics;
using Microsoft.UI.Xaml;
using VibesboxKiosk.Services;

namespace VibesboxKiosk;

public partial class App : Application
{
    // Started as early as possible (static ctor runs before App ctor) so we measure
    // process-start → dashboard-visible, not just App ctor onwards.
    public static readonly Stopwatch StartupStopwatch = Stopwatch.StartNew();

    private Window? _window;

    // Explicit static ctor: without it the field above may initialise lazily (at
    // first access), which made the startup timing read ~0 ms.
    static App() { }

    public App()
    {
        InitializeComponent();
        this.UnhandledException += App_UnhandledException;
        Log.Info($"[App] Starting — logs at {Log.Directory}");
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Error("[App] Unhandled exception", e.Exception);
        // An appliance: a UI glitch (e.g. in the settings page) must not take the
        // whole kiosk down. Log it and keep running.
        e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ConfigService.Instance.Load();
        ThemeService.Apply(ConfigService.Instance.Current.Theme);
        ConfigService.Instance.StartWatching();

        _window = new MainWindow();
        _window.Activate();
    }
}
